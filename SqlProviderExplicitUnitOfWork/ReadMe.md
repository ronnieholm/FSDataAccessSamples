This sample shows an explit implementation of the Unit of Work pattern via
the Entity Framework type provider. To isolate the application from knowing 
about the ORM used, we wrap the Entity Framework Object Context Unit of Work 
in our own.

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
        [Id]       INT IDENTITY (1, 1) NOT NULL,
        [Actor_Id] INT NOT NULL,
        [Movie_Id] INT NOT NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ActorsMovies_Actors] FOREIGN KEY ([ActorId]) REFERENCES [dbo].[Actors] ([Id]),
        CONSTRAINT [FK_ActorsMovies_Movies] FOREIGN KEY ([MovieId]) REFERENCES [dbo].[Movies] ([Id])
    );
