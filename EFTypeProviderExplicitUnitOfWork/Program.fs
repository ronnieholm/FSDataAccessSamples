namespace EFTypeProviderExplicitUnitOfWork

// Walkthrough: Accessing a SQL Database by Using Type Providers and Entities (F#)
// http://msdn.microsoft.com/en-us/library/hh361035%28v=VS.110%29.aspx

open System
open System.Data.Linq
open System.Data.Entity
open Microsoft.FSharp.Data.TypeProviders

[<AutoOpen>]
module Constants = 
    [<Literal>]
    let ConnectionString = "Data Source=(localdb)\Projects;Initial Catalog=EFExplicitUnitOfWork;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False"

type private EntityConnection = SqlEntityConnection<ConnectionString=ConnectionString, Pluralize = true>

module Program =
    [<EntryPoint>]
    let main args =
        // with EF type provider provided Unit of Work
        let uow = EntityConnection.GetDataContext()
        let a = EntityConnection.ServiceTypes.Actor(Name = "Sylvester Stallone", Born = DateTime(1946, 7, 6))
        uow.Actors.AddObject a

        let m = EntityConnection.ServiceTypes.Movie(Title = "Rambo")
        uow.Movies.AddObject m

        // manually specifying singular form of ActorsMovies isn't possible without the
        // use of a local schema file. Also the insertion fails with an exception
        // stating that metadata information is missing for the
        // SqlEntityConnection1.FK_ActorsMovies_Actors relation.
        //let am = EntityConnection.ServiceTypes.ActorsMovy(Actor = a, Movie = m)
        //uow.ActorsMovies.AddObject am
        uow.DataContext.SaveChanges() |> ignore

        let a' = 
            query { 
                for a in uow.Actors do 
                where(a.Name = "Sylvester Stallone") }
            |> Seq.head
        a'.Name <- "Sylvester Stallone II"
        uow.DataContext.SaveChanges() |> ignore

        0