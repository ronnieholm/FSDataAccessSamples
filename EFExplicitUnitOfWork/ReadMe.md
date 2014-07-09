This sample shows an explicit implementation of the Unit of Work pattern via
Entity Framework. To make the F# code more idiomatic, we wrap the Entity 
Framework DbContext Unit of Work in our own.

Before running the sample, create the following LocalDb. Since we aren't using
Entity Framework code-first, we need to alter the foreign keys to conform
to the framework convention over configuration naming schema (ActorId -> Actor_Id):

    CREATE TABLE [dbo].[Actors] (
        [Id]   INT            IDENTITY (1, 1) NOT NULL,
        [Name] NVARCHAR (MAX) NOT NULL,
        [Born] DATETIME       NOT NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE TABLE [dbo].[Movies] (
        [Id]    INT            IDENTITY (1, 1) NOT NULL,
        [Title] NVARCHAR (MAX) NOT NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE TABLE [dbo].[ActorsMovies] (
        [Id]       INT IDENTITY (1, 1) NOT NULL,
        [Actor_Id] INT NOT NULL,
        [Movie_Id] INT NOT NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ActorsMovies_Actors] FOREIGN KEY ([ActorId]) REFERENCES [dbo].[Actors] ([Id]),
        CONSTRAINT [FK_ActorsMovies_Movies] FOREIGN KEY ([MovieId]) REFERENCES [dbo].[Movies] ([Id])
    );
