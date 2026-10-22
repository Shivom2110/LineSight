/* =====================================================================
   LineSight - stored procedures
   Run after schema.sql. CREATE OR ALTER makes this file re-runnable.
   ===================================================================== */

USE LineSight;
GO

/* ---------------------------------------------------------------------
   usp_RecordMachineEvent - insert one PLC status report, return its Id.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_RecordMachineEvent
    @StationId      INT,
    @State          VARCHAR(10),
    @DowntimeReason NVARCHAR(100) = NULL,
    @GoodCount      INT,
    @ScrapCount     INT,
    @TimestampUtc   DATETIME2(3)
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Stations WHERE Id = @StationId)
        THROW 50001, 'Unknown station.', 1;

    INSERT INTO dbo.MachineEvents (StationId, State, DowntimeReason, GoodCount, ScrapCount, TimestampUtc)
    OUTPUT INSERTED.Id
    VALUES (@StationId, @State, @DowntimeReason, @GoodCount, @ScrapCount, @TimestampUtc);
END;
GO

/* ---------------------------------------------------------------------
   usp_GetStations - every station with its most recent state.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_GetStations
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  s.Id,
            s.Name,
            s.Sequence,
            CAST(s.IdealCycleTimeSeconds AS FLOAT) AS IdealCycleTimeSeconds,
            latest.State          AS CurrentState,
            latest.DowntimeReason AS DowntimeReason,
            latest.TimestampUtc   AS LastEventUtc
    FROM    dbo.Stations AS s
    -- OUTER APPLY + TOP (1) = "latest event per station" (a seek on IX_MachineEvents_Station_Timestamp).
    OUTER APPLY (
            SELECT TOP (1) e.State, e.DowntimeReason, e.TimestampUtc
            FROM   dbo.MachineEvents AS e
            WHERE  e.StationId = s.Id
            ORDER  BY e.TimestampUtc DESC, e.Id DESC
    ) AS latest
    ORDER BY s.Sequence;
END;
GO

/* ---------------------------------------------------------------------
   usp_GetStationOEE - aggregate one station's events over [@FromUtc, @ToUtc).

   Time accounting: each event "owns" the time until the next event
   (LEAD), and the last one owns the time until @ToUtc. A gap longer
   than @MaxGapSeconds means the PLC/station was offline (not scheduled),
   so it is capped instead of being counted as run time or downtime.

     PlannedProductionTime = time covered by any event
     RunTime               = time covered by Running events

   Result set 1: one row of aggregates + A/P/Q/OEE (empty if station unknown).
   Result set 2: downtime by reason (total seconds + number of episodes).

   The A/P/Q/OEE columns are for reports that query SQL directly;
   the API recomputes them from the aggregates with the unit-tested
   C# OeeCalculator, which also flags Performance > 100%.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_GetStationOEE
    @StationId     INT,
    @FromUtc       DATETIME2(3),
    @ToUtc         DATETIME2(3),
    @MaxGapSeconds INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    IF @ToUtc <= @FromUtc
        THROW 50010, '@ToUtc must be later than @FromUtc.', 1;

    DECLARE @MaxGapMs BIGINT = CAST(@MaxGapSeconds AS BIGINT) * 1000;

    WITH Ordered AS
    (
        SELECT  e.State,
                e.DowntimeReason,
                e.GoodCount,
                e.ScrapCount,
                e.TimestampUtc,
                LEAD(e.TimestampUtc) OVER (ORDER BY e.TimestampUtc, e.Id) AS NextTimestampUtc,
                LAG(e.State)         OVER (ORDER BY e.TimestampUtc, e.Id) AS PrevState,
                LAG(e.DowntimeReason) OVER (ORDER BY e.TimestampUtc, e.Id) AS PrevReason
        FROM    dbo.MachineEvents AS e
        WHERE   e.StationId = @StationId
          AND   e.TimestampUtc >= @FromUtc
          AND   e.TimestampUtc <  @ToUtc
    )
    SELECT  o.State,
            o.DowntimeReason,
            o.GoodCount,
            o.ScrapCount,
            CASE WHEN gap.Ms > @MaxGapMs THEN @MaxGapMs ELSE gap.Ms END AS DurationMs,
            -- A new downtime episode starts when we enter Down, or the reason changes while Down.
            CASE WHEN o.State = 'Down'
                  AND (o.PrevState IS NULL OR o.PrevState <> 'Down' OR o.PrevReason <> o.DowntimeReason)
                 THEN 1 ELSE 0 END AS IsEpisodeStart
    INTO    #Durations
    FROM    Ordered AS o
    CROSS APPLY (SELECT DATEDIFF_BIG(MILLISECOND, o.TimestampUtc, COALESCE(o.NextTimestampUtc, @ToUtc)) AS Ms) AS gap;

    DECLARE @PlannedSeconds FLOAT, @RunSeconds FLOAT, @Good INT, @Scrap INT;

    SELECT  @PlannedSeconds = COALESCE(SUM(DurationMs), 0) / 1000.0,
            @RunSeconds     = COALESCE(SUM(CASE WHEN State = 'Running' THEN DurationMs ELSE 0 END), 0) / 1000.0,
            @Good           = COALESCE(SUM(GoodCount), 0),
            @Scrap          = COALESCE(SUM(ScrapCount), 0)
    FROM    #Durations;

    -- Result set 1: aggregates + factors (divide-by-zero returns 0, same as the C# code).
    SELECT  s.Id   AS StationId,
            s.Name AS StationName,
            CAST(s.IdealCycleTimeSeconds AS FLOAT) AS IdealCycleTimeSeconds,
            @PlannedSeconds   AS PlannedProductionTimeSeconds,
            @RunSeconds       AS RunTimeSeconds,
            @Good             AS GoodCount,
            @Scrap            AS ScrapCount,
            @Good + @Scrap    AS TotalCount,
            ROUND(f.Availability, 4)                              AS Availability,
            ROUND(f.Performance, 4)                               AS Performance,
            ROUND(f.Quality, 4)                                   AS Quality,
            ROUND(f.Availability * f.Performance * f.Quality, 4)  AS OEE
    FROM    dbo.Stations AS s
    CROSS APPLY (
            SELECT  CASE WHEN @PlannedSeconds = 0 THEN 0
                         WHEN @RunSeconds > @PlannedSeconds THEN 1
                         ELSE @RunSeconds / @PlannedSeconds END                    AS Availability,
                    CASE WHEN @RunSeconds = 0 THEN 0
                         ELSE CAST(s.IdealCycleTimeSeconds AS FLOAT) * (@Good + @Scrap) / @RunSeconds END AS Performance,
                    CASE WHEN @Good + @Scrap = 0 THEN 0
                         ELSE CAST(@Good AS FLOAT) / (@Good + @Scrap) END            AS Quality
    ) AS f
    WHERE   s.Id = @StationId;

    -- Result set 2: downtime Pareto for this station and window.
    SELECT  DowntimeReason                        AS Reason,
            SUM(DurationMs) / 1000.0              AS DurationSeconds,
            SUM(IsEpisodeStart)                   AS Occurrences
    FROM    #Durations
    WHERE   State = 'Down'
    GROUP BY DowntimeReason
    ORDER BY DurationSeconds DESC;
END;
GO

/* ---------------------------------------------------------------------
   usp_RecordModuleStep - store one genealogy step and the module's new
   status in a single transaction. Creates the module on its first step.
   Routing rules are enforced in C# (TraceabilityService) before this runs;
   UQ_ModuleSteps_Module_Station is the database-level backstop.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_RecordModuleStep
    @SerialNumber VARCHAR(20),
    @StationId    INT,
    @Result       VARCHAR(5),
    @TimestampUtc DATETIME2(3),
    @NewStatus    VARCHAR(12)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @ModuleId INT;

        -- UPDLOCK + HOLDLOCK stops two concurrent calls both deciding the serial is new.
        SELECT @ModuleId = Id
        FROM   dbo.Modules WITH (UPDLOCK, HOLDLOCK)
        WHERE  SerialNumber = @SerialNumber;

        IF @ModuleId IS NULL
        BEGIN
            INSERT INTO dbo.Modules (SerialNumber, Status, CreatedUtc)
            VALUES (@SerialNumber, @NewStatus, @TimestampUtc);

            SET @ModuleId = CAST(SCOPE_IDENTITY() AS INT);
        END
        ELSE
        BEGIN
            UPDATE dbo.Modules SET Status = @NewStatus WHERE Id = @ModuleId;
        END;

        INSERT INTO dbo.ModuleSteps (ModuleId, StationId, Result, TimestampUtc)
        VALUES (@ModuleId, @StationId, @Result, @TimestampUtc);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        -- Undo the status change if the step insert failed, even when the caller catches the error.
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

/* ---------------------------------------------------------------------
   usp_GetModuleGenealogy - a module and its steps in line order.
   Result set 1: module header (empty if the serial is unknown).
   Result set 2: steps ordered by station sequence.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_GetModuleGenealogy
    @SerialNumber VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  m.SerialNumber, m.Status, m.CreatedUtc
    FROM    dbo.Modules AS m
    WHERE   m.SerialNumber = @SerialNumber;

    SELECT  st.Id       AS StationId,
            st.Name     AS StationName,
            st.Sequence AS StationSequence,
            ms.Result,
            ms.TimestampUtc
    FROM    dbo.Modules     AS m
    JOIN    dbo.ModuleSteps AS ms ON ms.ModuleId = m.Id
    JOIN    dbo.Stations    AS st ON st.Id = ms.StationId
    WHERE   m.SerialNumber = @SerialNumber
    ORDER BY st.Sequence, ms.TimestampUtc;
END;
GO
