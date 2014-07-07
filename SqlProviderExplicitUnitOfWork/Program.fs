namespace SqlProviderExplicitUnitOfWork

open System
open System.Linq
open FSharp.Data.Sql

[<AutoOpen>]
module Constants = 
    [<Literal>]
    let ConnectionString = "Data Source=(localdb)\Projects;Initial Catalog=EFExplicitUnitOfWork;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False"

type sql = 
    SqlDataProvider< 
        ConnectionString = ConnectionString,
        DatabaseVendor = Common.DatabaseProviderTypes.MSSQLSERVER,
        IndividualsAmount = 1000,
        UseOptionTypes = true>

module Program =
    [<EntryPoint>]
    let main args =
        let ctx = sql.GetDataContext()

        let a = ctx.``[dbo].[Actors]``.Create()
        a.Name <- "Sylvester Stallone"
        a.Born <- DateTime(1946, 7, 6)
        
        let m = ctx.``[dbo].[Movies]``.Create()
        m.Title <- "Rambo"

        // setting up the relationship isn't possible unless we control the 
        // primary key, e.g., using guids?
        let am = ctx.``[dbo].[ActorsMovies]``.Create()
        
        // properties cannot be set
        // am.FK_ActorsMovies_Actors <- a
        // am.FK_ActorsMovies_Movies <- m

        // what to set the Ids to?       
        //am.Actor_Id <- a.Id
        //am.Movie_Id <- a.Id

        ctx.SubmitUpdates()

        let a' = 
            query { 
                for a in ctx.``[dbo].[Actors]`` do 
                where(a.Name = "Sylvester Stallone")
                select a }
            |> Seq.head
        a'.Name <- "Sylvester Stallone II"
        ctx.SubmitUpdates()

        0