namespace EFExplicitUnitOfWork

// http://blogs.msdn.com/b/visualstudio/archive/2011/04/04/f-code-first-development-with-entity-framework-4-1.aspx

open System
open System.Data.Entity
open System.Linq

[<AutoOpen>]
module Constants = 
    let ConnectionString = "Data Source=(localdb)\Projects;Initial Catalog=EFExplicitUnitOfWork;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False"

[<AllowNullLiteral>]
type Actor() = 
    member val Id = 0 with get, set
    member val Name = "" with get, set
    member val Born = DateTime() with get, set

[<AllowNullLiteral>]
type Movie() = 
    member val Id = 0 with get, set
    member val Title = "" with get, set

[<AllowNullLiteral>]
type ActorsMovies() =
    member val Id = 0 with get, set
    member val Actor = null with get, set
    member val Movie = null with get, set

type public Db() =
    inherit DbContext(ConnectionString)

    [<DefaultValue>]
    val mutable private _actors : DbSet<Actor>
    member public x.Actors with get() = x._actors and set v = x._actors <- v

    [<DefaultValue>]
    val mutable private _movies : DbSet<Movie>
    member public x.Movies with get() = x._movies and set v = x._movies <- v    

    [<DefaultValue>]
    val mutable private _actorsMovies : DbSet<ActorsMovies>
    member public x.ActorsMovies with get() = x._actorsMovies and set v = x._actorsMovies <- v    

type UnitOfWork() =
    let db = new Db()

    // for querying we could define Get methods that return List<T> such that
    // the client is persistence ignorant. But that's a lot of extra work with
    // little actual benefit. We accept the leaking abstraction and expose the
    // DbSet<T> for querying to take advantage of the LINQ methods even though
    // it makes it difficult to fake the database in tests.
    member x.Actors = db.Actors
    member x.Movies = db.Movies
    member x.ActorsMovies = db.ActorsMovies

    // makes the syntax for working with EF UoW nicer
    member x.Add a = db.Actors.Add a |> ignore
    member x.Add m = db.Movies.Add m |> ignore
    member x.Add am = db.ActorsMovies.Add am |> ignore
    member x.Remove a = db.Actors.Remove a |> ignore
    member x.Remove m = db.Movies.Remove m |> ignore
    member x.Remove am = db.ActorsMovies.Remove am |> ignore    
    member x.SaveChanges() = db.SaveChanges() |> ignore 

    interface IDisposable with
        member x.Dispose() =
            db.Dispose()

module Program =
    [<EntryPoint>]
    let main args =
        // with EF provided Unit of Work
        use db = new Db()

        let a = Actor(Name = "Sylvester Stallone", Born = DateTime(1946, 7, 6))
        db.Actors.Add a |> ignore

        let m = Movie(Title = "Rambo")
        db.Movies.Add m |> ignore

        let am = ActorsMovies(Actor = a, Movie = m)
        db.ActorsMovies.Add am |> ignore 

        db.SaveChanges() |> ignore

        let a' = db.Actors.First(fun a -> a.Name = "Sylvester Stallone")
        a'.Name <- "Sylvester Stallone II"
        db.SaveChanges() |> ignore
        
        // with custom Unit of Work wrapping EF Unit of Work
        use uow = new UnitOfWork()
        uow.Add a
        uow.Add m
        uow.Add am

        let a' = uow.Actors.First(fun a -> a.Name = "Sylvester Stallone")
        a'.Name <- "Sylvester Stallone III"
        uow.SaveChanges()

        0