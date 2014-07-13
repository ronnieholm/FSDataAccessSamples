namespace SqlCommandProviderExplicitUnitOfWork

// http://blogs.msdn.com/b/fsharpteam/archive/2014/05/23/fsharp-data-sqlclient-seamlessly-integrating-sql-and-f-in-the-same-code-base-guest-post.aspx
// http://fsprojects.github.io/FSharp.Data.SqlClient

// Repositories On Top UnitOfWork Are Not a Good Idea
// http://www.wekeroad.com/2014/03/04/repositories-and-unitofwork-are-not-a-good-idea

// Favor query objects over repositories
// http://lostechies.com/jimmybogard/2012/10/08/favor-query-objects-over-repositories/

// todo: use Choice type instead of Some to convey information about error to caller

open System
open System.Data.SqlClient
open FSharp.Data

[<AutoOpen>]
module Queries = 
    [<Literal>]
    let ConnectionString = "Data Source=(localdb)\Projects;Initial Catalog=EFExplicitUnitOfWork;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False"

    // writing this code is the price we pay for compile-time checking (also allows for hand-tweeking SQL)
    type ActorById = SqlCommandProvider<"select * from Actors where Id = @id", ConnectionString, SingleRow = true>
    type CreateActor = SqlCommandProvider<"insert into Actors (Name, Born) values (@name, @born) select cast(scope_identity() as int)", ConnectionString, SingleRow = true>
    type UpdateActor = SqlCommandProvider<"update Actors set Name = @name, Born = @born where Id = @id", ConnectionString>
    type DeleteActor = SqlCommandProvider<"delete from Actors where Id = @id", ConnectionString>

    type MovieById = SqlCommandProvider<"select * from Movies where Id = @id", ConnectionString, SingleRow = true>
    type CreateMovie = SqlCommandProvider<"insert into Movies (Title) values (@title) select cast(scope_identity() as int)", ConnectionString, SingleRow = true>
    type UpdateMovie = SqlCommandProvider<"update Movies set Title = @title where Id = @id", ConnectionString>
    type DeleteMovie = SqlCommandProvider<"delete from Movies where Id = @id", ConnectionString>

    type ActorMovieById = SqlCommandProvider<"select * from ActorsMovies where Id = @id", ConnectionString, SingleRow = true>
    type ActorMovieByActorId = SqlCommandProvider<"select * from ActorsMovies where Actor_Id = @id", ConnectionString>
    type ActorMovieByMovieId = SqlCommandProvider<"select * from ActorsMovies where Movie_Id = @id", ConnectionString>
    type CreateActorMovie = SqlCommandProvider<"insert into ActorsMovies (Actor_Id, Movie_Id) values (@actorId, @movieId) select cast(scope_identity() as int)", ConnectionString, SingleRow = true>
    type UpdateActorMovie = SqlCommandProvider<"update ActorsMovies set Actor_Id = @actorId, Movie_Id = @movieId where Id = @id", ConnectionString>
    type DeleteActorMovie = SqlCommandProvider<"delete from ActorsMovies where Id = @id", ConnectionString>

    let toOption n = if n = 0 then None else Some n

// Take 1 -- sub-optimal

// we're creating an abstraction (repository) of an abstraction (SqlClient) here.
// Skip the repository and use SqlClient directly for querying and use transaction 
// object for updating.
type ActorRepository(t: SqlTransaction) =
    member __.GetById id = (new ActorById()).Execute(id)
    member __.Create (a: ActorById.Record) = (new CreateActor(t)).Execute(name = a.Name, born = a.Born) |> Option.get
    member __.Update (a: ActorById.Record) = (new UpdateActor(t)).Execute(id = a.Id, name = a.Name, born = a.Born) |> toOption
    member __.Delete (a: ActorById.Record) = (new DeleteActor(t)).Execute(a.Id) |> toOption

type MovieRepository(t: SqlTransaction) =
    member __.GetById id = (new MovieById()).Execute(id) 
    member __.Create (m: MovieById.Record) = (new CreateMovie(t)).Execute(title = m.Title) |> Option.get
    member __.Update (m: MovieById.Record) = (new UpdateMovie(t)).Execute(id = m.Id, title = m.Title) |> toOption
    member __.Delete (m: MovieById.Record) = (new DeleteMovie(t)).Execute(m.Id) |> toOption

// what to do when a query method doesn't fit into a specific repository?
// Create a query object CQRS-style
type ActorMovieRepository(t: SqlTransaction) =
    member __.GetById id = (new ActorMovieById(t)).Execute(id) 
    member __.GetByActorId id = (new ActorMovieByActorId(t)).Execute(id) |> Seq.toList
    member __.GetByMovieId id = (new ActorMovieByMovieId(t)).Execute(id) |> Seq.toList   
    member __.Create (am: ActorMovieById.Record) = (new CreateActorMovie(t)).Execute(actorId = am.Actor_Id, movieId = am.Movie_Id) |> Option.get
    member __.Update (am: ActorMovieById.Record) = (new UpdateActorMovie(t)).Execute(id = am.Id, actorId = am.Actor_Id, movieId = am.Movie_Id) |> toOption
    member __.Delete (am: ActorMovieById.Record) = (new DeleteActorMovie(t)).Execute(am.Id) |> toOption

type UnitOfWork() =
    // watch out for transaction isolation level: we don't want to 
    // read uncommitted data from other transactions
    let con = new SqlConnection(ConnectionString)
    let trans =
        con.Open()
        con.BeginTransaction()

    member __.Actors = ActorRepository(trans)
    member __.Movies = MovieRepository(trans)
    member __.ActorsMovies = ActorMovieRepository(trans)
    member __.Commit() = 
        try
            try
                trans.Commit()
            with
            | _ -> 
                trans.Rollback()
                reraise()
        finally
            con.Close()

    interface IDisposable with
        member __.Dispose() =
            con.Dispose()

// Take 2 -- better

// every command is an isolated unit so we don't pass in a transaction. 
// Instead each command would do its work in its own transaction.
// Real world commands would be more complex.
type CreateActorCommand(a: ActorById.Record) =
    member __.Execute() =
        (new CreateActor()).Execute(name = a.Name, born = a.Born) |> Option.get

type CreateMovieCommand(m: MovieById.Record) =
    member __.Execute() =
        (new CreateMovie()).Execute(title = m.Title) |> Option.get

type CreateActorMovieCommand(a: ActorById.Record, m: MovieById.Record) =
    member __.Execute() =
        (new CreateActorMovie()).Execute(actorId = a.Id, movieId = m.Id) |> Option.get

// add query types for complex queries used in multiple places in a real world app

module Program =
    [<EntryPoint>]
    let main args =
        // take 1
        use uow = new UnitOfWork()
        let stallone =  ActorById.Record(id = 0, name = "Sylvester Stallone", born = DateTime(1946, 7, 6))
        let aId = 
            uow.Actors.Create stallone
            |> function
                | Some id -> id
                | None -> failwith "Actor not inserted"

        let rambo = MovieById.Record(id = 0, title = "Rambo")
        let mId = 
            uow.Movies.Create rambo
            |> function
                | Some id -> id
                | None -> failwith "Movie not inserted"

        let stalloneRambo = ActorMovieById.Record(id = 0, actor_Id = aId, movie_Id = mId)
        let amId = 
            uow.ActorsMovies.Create stalloneRambo
            |> function
                | Some id -> id
                | None -> failwith "ActorMovie not inserted"

        uow.Commit()

        // take 2
        let aId = 
            CreateActorCommand(ActorById.Record(id = 0, name = "Sly", born = DateTime(1946, 7, 6))).Execute()
            |> function
                | Some id -> id
                | None -> failwith "Actor not inserted"

        let mId = 
            CreateMovieCommand(MovieById.Record(id = 0, title = "Rambo II")).Execute()
            |> function
                | Some id -> id
                | None -> failwith "Movie not inserted"
        
        let a = (new ActorById()).Execute(aId) |> Option.get
        let m = (new MovieById()).Execute(mId) |> Option.get
        let amId = 
            CreateActorMovieCommand(a, m).Execute()
            |> function
                | Some id -> id
                | None -> failwith "ActorMovie not inserted"

        0