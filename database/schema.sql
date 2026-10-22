/* =====================================================================
   LineSight - schema + seed data
   Target: SQL Server 2019+ / LocalDB / Azure SQL Edge
   Re-runnable: drops and recreates all tables (DEV ONLY - deletes data).
   ===================================================================== */

IF DB_ID(N'LineSight') IS NULL
    CREATE DATABASE LineSight;
GO

USE LineSight;
GO

-- Drop children before parents because of the foreign keys.
DROP TABLE IF EXISTS dbo.ModuleSteps;
DROP TABLE IF EXISTS dbo.Modules;
DROP TABLE IF EXISTS dbo.MachineEvents;
DROP TABLE IF EXISTS dbo.Stations;
GO

/* ---------------------------------------------------------------------
   Stations: the 5 process steps of a thin-film module line.
   Id is assigned explicitly (not IDENTITY) so it is stable across environments.
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Stations
(
    Id                    INT           NOT NULL CONSTRAINT PK_Stations PRIMARY KEY,
    Name                  NVARCHAR(50)  NOT NULL CONSTRAINT UQ_Stations_Name UNIQUE,
    Sequence              INT           NOT NULL CONSTRAINT UQ_Stations_Sequence UNIQUE,
    IdealCycleTimeSeconds DECIMAL(9,3)  NOT NULL
        CONSTRAINT CK_Stations_IdealCycleTime CHECK (IdealCycleTimeSeconds > 0)
);
GO

/* ---------------------------------------------------------------------
   MachineEvents: one row per PLC status report (high volume, append-only).
   GoodCount/ScrapCount are parts finished since the previous report.
   --------------------------------------------------------------------- */
CREATE TABLE dbo.MachineEvents
(
    Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MachineEvents PRIMARY KEY,
    StationId      INT           NOT NULL CONSTRAINT FK_MachineEvents_Stations REFERENCES dbo.Stations (Id),
    State          VARCHAR(10)   NOT NULL
        CONSTRAINT CK_MachineEvents_State CHECK (State IN ('Running', 'Idle', 'Down')),
    DowntimeReason NVARCHAR(100) NULL,
    GoodCount      INT           NOT NULL CONSTRAINT CK_MachineEvents_GoodCount CHECK (GoodCount >= 0),
    ScrapCount     INT           NOT NULL CONSTRAINT CK_MachineEvents_ScrapCount CHECK (ScrapCount >= 0),
    TimestampUtc   DATETIME2(3)  NOT NULL,

    -- Same rule as MachineEventValidator in C#: a reason if and only if the station is Down.
    CONSTRAINT CK_MachineEvents_DowntimeReason CHECK (
        (State = 'Down' AND DowntimeReason IS NOT NULL) OR
        (State <> 'Down' AND DowntimeReason IS NULL))
);
GO

-- Serves both "events for station X between two times" (OEE) and "latest event per station".
-- INCLUDE makes it covering, so those queries never touch the base table.
CREATE INDEX IX_MachineEvents_Station_Timestamp
    ON dbo.MachineEvents (StationId, TimestampUtc)
    INCLUDE (State, DowntimeReason, GoodCount, ScrapCount);
GO

/* ---------------------------------------------------------------------
   Modules: one row per serialized solar module.
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Modules
(
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Modules PRIMARY KEY,
    SerialNumber VARCHAR(20)  NOT NULL CONSTRAINT UQ_Modules_SerialNumber UNIQUE,   -- unique index = fast lookup by serial
    Status       VARCHAR(12)  NOT NULL
        CONSTRAINT CK_Modules_Status CHECK (Status IN ('InProgress', 'Completed', 'Scrapped')),
    CreatedUtc   DATETIME2(3) NOT NULL
);
GO

/* ---------------------------------------------------------------------
   ModuleSteps: the genealogy - every station a module passed through.
   --------------------------------------------------------------------- */
CREATE TABLE dbo.ModuleSteps
(
    Id           BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ModuleSteps PRIMARY KEY,
    ModuleId     INT          NOT NULL CONSTRAINT FK_ModuleSteps_Modules REFERENCES dbo.Modules (Id),
    StationId    INT          NOT NULL CONSTRAINT FK_ModuleSteps_Stations REFERENCES dbo.Stations (Id),
    Result       VARCHAR(5)   NOT NULL CONSTRAINT CK_ModuleSteps_Result CHECK (Result IN ('Pass', 'Scrap')),
    TimestampUtc DATETIME2(3) NOT NULL,

    -- Database-level backstop for the routing rules: a module visits each station at most once.
    -- Its index (ModuleId first) also serves the genealogy lookup.
    CONSTRAINT UQ_ModuleSteps_Module_Station UNIQUE (ModuleId, StationId)
);
GO

-- "Which modules went through station X in this period?" (e.g. for a quality containment).
CREATE INDEX IX_ModuleSteps_Station_Timestamp ON dbo.ModuleSteps (StationId, TimestampUtc);
GO

/* ---------------------------------------------------------------------
   Seed data
   --------------------------------------------------------------------- */
INSERT INTO dbo.Stations (Id, Name, Sequence, IdealCycleTimeSeconds)
VALUES
    (1, N'Deposition',      1, 2.000),
    (2, N'Laser Scribe',    2, 1.800),
    (3, N'Lamination',      3, 2.000),
    (4, N'Electrical Test', 4, 1.500),
    (5, N'Pack',            5, 1.500);
GO
