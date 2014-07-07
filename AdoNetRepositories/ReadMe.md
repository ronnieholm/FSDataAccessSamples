This sample shows a couple of repositories implemented using low-level ADO.NET 
classes that share an SqlConnection/SqlTransaction and thus provides a crude 
implementation of the Unit of Work pattern.

Before running the sample, create the following LocalDb:

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
        [Id]      INT IDENTITY (1, 1) NOT NULL,
        [ActorId] INT NOT NULL,
        [MovieId] INT NOT NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ActorsMovies_Actors] FOREIGN KEY ([ActorId]) REFERENCES [dbo].[Actors] ([Id]),
        CONSTRAINT [FK_ActorsMovies_Movies] FOREIGN KEY ([MovieId]) REFERENCES [dbo].[Movies] ([Id])
    );

