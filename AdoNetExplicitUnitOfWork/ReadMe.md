This sample shows an explit implementation of the Unit of Work pattern, making
use of ADO.NET classes and repositories underneath. Looking at the Unit of
Work class, notice how it starts to resemble the DbContext (derived class) 
from Entity Framework.

Before running the sample, create the following LocalDb:

    CREATE TABLE [dbo].[Actors] (
        [Id]   UNIQUEIDENTIFIER NOT NULL,
        [Name] NVARCHAR (MAX)   NOT NULL,
        [Born] DATETIME         NOT NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE TABLE [dbo].[Movies] (
        [Id]    UNIQUEIDENTIFIER NOT NULL,
        [Title] NVARCHAR (MAX)   NOT NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE TABLE [dbo].[ActorsMovies] (
        [Id]      UNIQUEIDENTIFIER NOT NULL,
        [ActorId] UNIQUEIDENTIFIER NOT NULL,
        [MovieId] UNIQUEIDENTIFIER NOT NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ActorsMovies_Actors] FOREIGN KEY ([ActorId]) REFERENCES [dbo].[Actors] ([Id]),
        CONSTRAINT [FK_ActorsMovies_Movies] FOREIGN KEY ([MovieId]) REFERENCES [dbo].[Movies] ([Id])
    );