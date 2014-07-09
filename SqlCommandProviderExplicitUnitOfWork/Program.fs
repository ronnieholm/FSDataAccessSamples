namespace SqlCommandProviderExplicitUnitOfWork

// http://blogs.msdn.com/b/fsharpteam/archive/2014/05/23/fsharp-data-sqlclient-seamlessly-integrating-sql-and-f-in-the-same-code-base-guest-post.aspx
// http://fsprojects.github.io/FSharp.Data.SqlClient

// Repositories On Top UnitOfWork Are Not a Good Idea
// http://www.wekeroad.com/2014/03/04/repositories-and-unitofwork-are-not-a-good-idea

// Favor query objects over repositories
// http://lostechies.com/jimmybogard/2012/10/08/favor-query-objects-over-repositories/

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

// instead of creating classes with data annotations, a more functional style
// would be to prevent the creation of an object with an invalid state in the 
// first place using railway oriented programming.

// compared to regular classes record types have build-in immutability,
// equality and comparison
type Actor =
    { Id: int
      Name: string
      Born: DateTime }

type Movie = 
    { Id: int
      Title: string }

type ActorMovie =
    { Id: int
      ActorId: int
      MovieId: int }

type ActorRepository(t: SqlTransaction) =
    member __.GetById id = 
        (new ActorById()).Execute(id) 
        |> function
            | Some a -> Some { Id = a.Id; Name = a.Name; Born = a.Born }
            | None -> None

    member __.Create a = (new CreateActor(t)).Execute(name = a.Name, born = a.Born) |> Option.get
    member __.Update (a: Actor) = (new UpdateActor(t)).Execute(id = a.Id, name = a.Name, born = a.Born) |> toOption
    member __.Delete a = (new DeleteActor(t)).Execute(a.Id) |> toOption

type MovieRepository(t: SqlTransaction) =
    member __.GetById id = 
        (new MovieById()).Execute(id) 
        |> function
            | Some m -> Some { Id = m.Id; Title = m.Title }
            | None -> None

    member __.Create m = (new CreateMovie(t)).Execute(title = m.Title) |> Option.get
    member __.Update (m: Movie) = (new UpdateMovie(t)).Execute(id = m.Id, title = m.Title) |> toOption
    member __.Delete m = (new DeleteMovie(t)).Execute(m.Id) |> toOption

// what to do when a query method doesn't fit into a specific repository?
// Create a query object CQRS-style
type ActorMovieRepository(t: SqlTransaction) =
    member __.GetById id =
        (new ActorMovieById(t)).Execute(id) 
        |> function
            | Some am -> Some { Id = am.Id; ActorId = am.Actor_Id; MovieId = am.Movie_Id }
            | None -> None

    member __.GetByActorId id =
        (new ActorMovieByActorId(t)).Execute(id) 
        |> Seq.map (fun r -> { Id = r.Id; ActorId = r.Actor_Id; MovieId = r.Movie_Id })
        |> Seq.toList

    member __.GetByMovieId id =
        (new ActorMovieByMovieId(t)).Execute(id) 
        |> Seq.map (fun x -> { Id = x.Id; ActorId = x.Actor_Id; MovieId = x.Movie_Id }) 
        |> Seq.toList   

    member __.Create am = (new CreateActorMovie(t)).Execute(actorId = am.ActorId, movieId = am.MovieId) |> Option.get
    member __.Update (am: ActorMovie) = (new UpdateActorMovie(t)).Execute(id = am.Id, actorId = am.ActorId, movieId = am.MovieId) |> toOption
    member __.Delete am = (new DeleteActorMovie(t)).Execute(am.Id) |> toOption

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

module Program =
    [<EntryPoint>]
    let main args =
        use uow = new UnitOfWork()
        let stallone = { Id = 0; Name = "Sylvester Stallone"; Born = DateTime(1946, 7, 6) }
        let aId = 
            uow.Actors.Create stallone
            |> function
                | Some id -> id
                | None -> failwith "Actor not inserted"

        let rocky =  { Id = 0; Title = "Rambo" }
        let mId = 
            uow.Movies.Create rocky
            |> function
                | Some id -> id
                | None -> failwith "Movie not inserted"

        let stalloneRambo = { Id = 0; ActorId = aId; MovieId = mId }
        let amId = 
            uow.ActorsMovies.Create stalloneRambo
            |> function
                | Some id -> id
                | None -> failwith "ActorMovie not inserted"

        uow.Commit()
        0