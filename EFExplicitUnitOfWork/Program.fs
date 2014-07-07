namespace EFExplicitUnitOfWork

open System
open System.Data.Entity

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

module Program =
    [<EntryPoint>]
    let main args =
        use db = new Db()

        let a = Actor(Name = "Sylvester Stallone", Born = DateTime(1946, 7, 6))
        db.Actors.Add a |> ignore

        let m = Movie(Title = "Rambo")
        db.Movies.Add m |> ignore

        let am = ActorsMovies(Actor = a, Movie = m)
        db.ActorsMovies.Add am |> ignore 

        db.SaveChanges() |> ignore
        0



