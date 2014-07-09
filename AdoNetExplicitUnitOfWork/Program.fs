namespace AdoNetExplicitUnitOfWork

// Repositories On Top UnitOfWork Are Not a Good Idea
// http://www.wekeroad.com/2014/03/04/repositories-and-unitofwork-are-not-a-good-idea

open System
open System.Data
open System.Linq
open System.Collections.Generic
open System.Data.SqlClient

[<AutoOpen>]
module Constants = 
    let ConnectionString = "Data Source=(localdb)\Projects;Initial Catalog=AdoNetExplicitUnitOfWork;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False"

type Actor = 
    { Id: Guid
      Name: string
      Born: DateTime }

type Movie = 
    { Id: Guid
      Title: string }

type ActorMovie =
    { Id: Guid
      ActorId: Guid
      MovieId: Guid }

// supported types of entities
type Entity =
    | Actor of Actor
    | Movie of Movie
    | ActorMovie of ActorMovie

[<AutoOpen>]
module Helpers =
    let setParam (cmd: SqlCommand) name value = cmd.Parameters.AddWithValue(name, value) |> ignore

// Similar to the Entity Framework DbContext class.
// See description of Unit of Work pattern in Fowler's 
// Patterns of Enterprise Application Architecture, page 184

// for testing the domain, we could've UnitOfWork implement
// an interface that we'd then implement in the test to
// work against an in-memory database
type UnitOfWork() =
    let updated = List<_>()
    let added = List<_>()
    let deleted = List<_>()
    
    let identityMap = Dictionary<_, _>()
    let mkCmd con trans sql = new SqlCommand(sql, con, trans)    

    // avoids inconsistent reads by registering clean objects whenever
    // they're read from the database. If we wanted to, during commit
    // we could find the original object and do a selective update of
    // only those fields that were actually changed.
    // Fowler names this method registerClean in his UoW pattern.
    // We could've created multiple identity maps, storing each map
    // inside the corresponding repository.
    member __.AddToIdentityMap e =
        match e with
        | Actor a -> identityMap.Add(a.Id, e)
        | Movie m -> identityMap.Add(m.Id, e)
        | ActorMovie am -> identityMap.Add(am.Id, e)

    member __.RemoveFromIdentityMap e =
        match e with
        | Actor a -> identityMap.Remove a.Id
        | Movie m -> identityMap.Remove m.Id
        | ActorMovie am -> identityMap.Remove am.Id

    member __.GetFromIdentityMap id =
        if identityMap.ContainsKey id 
        then Some identityMap.[id]
        else None

    // These methods are for caller registration of objects for the UoW to 
    // keep track of during the business transaction
    member __.Update e =
        if updated.Contains e then failwith "Can't update the same object more than once"
        if deleted.Contains e then failwith "Can't update deleted object"

        // it may be valid to allow for this
        //if added.Contains e then failwith "Can't update object also added"
        updated.Add e

    member x.Add e =
        // no need to assert that id has been set as it's enforced through use of record type
        if updated.Contains e then failwith "Can't add existing object as new"
        if deleted.Contains e then failwith "Can't add deleted object as new"
        if added.Contains e then failwith "Can't add the same object more than once"
        added.Add e
        x.AddToIdentityMap e

    member x.Delete e =
        if added.Contains e then added.Remove e |> ignore
        if updated.Contains e then updated.Remove e |> ignore
        if not (deleted.Contains e) then deleted.Add e
        x.RemoveFromIdentityMap e

    // repositories serving the same purpose as DbSet<T> in an EF DbContext derived class
    member x.Actors = ActorRepository x
    member x.Movies = MovieRepository x
    member x.ActorsMovies = ActorsMoviesRepository x

    member x.Commit() = 
        use con = new SqlConnection(ConnectionString)
        con.Open()
        let trans = con.BeginTransaction()

        // we could iterate the entire identity map to detect
        // inconsistent reads and fail the transaction, but in most
        // cases it suffices to check for inconsistencies of data modified,
        // i.e., as part of update and delete (or a subset of the identity
        // map perhaps stored in a seperate watch list)
        let mkCmd' = mkCmd con trans
        try
            try
                for e in added do
                    match e with
                    | Actor a -> x.Actors.Create a mkCmd'                   
                    | Movie m -> x.Movies.Create m mkCmd'
                    | ActorMovie am -> x.ActorsMovies.Create am mkCmd'
                for e in updated do
                    match e with
                    | Actor a -> x.Actors.Update a mkCmd'
                    | Movie m -> x.Movies.Update m mkCmd'
                    | ActorMovie am -> x.ActorsMovies.Update am mkCmd'
                for e in deleted do
                    match e with
                    | Actor a -> x.Actors.Delete a mkCmd'
                    | Movie m -> x.Movies.Delete m mkCmd'
                    | ActorMovie am -> x.ActorsMovies.Delete am mkCmd'

                trans.Commit()
            with
            | ex -> 
                trans.Rollback()
                reraise()
        finally
            con.Close()

// data mapper and repository in one type
and ActorRepository(uow) =
    let map (r: SqlDataReader) =
        seq {
            while r.Read() do
                yield { Id = r.["Id"] :?> Guid
                        Name = r.["Name"] :?> string
                        Born = r.["Born"] :?> DateTime } } |> Seq.toList

    member private __.GetInternalById id (mkCmd: string -> SqlCommand) =
        use con = new SqlConnection(ConnectionString)
        con.Open()
        use cmd = mkCmd "select * from Actors where Id = @id"
        setParam cmd "@id" id
        use reader = cmd.ExecuteReader()
        let actors = map reader
        if actors.Count() = 1 then Some (Actor (actors |> List.head))
        else None

    // having an SQL builder available would simplify get methods on different
    // criteria. That's what EF and LINQ gives us, i.e., a single DbSet<T> to
    // form queries over without the need for individual and repetitive get methods
    member x.GetById id =
        // if we where to fetch related object beware of n + 1 problem. We'd
        // need a way to definere which objects to include and do cycle-detection
        match uow.GetFromIdentityMap id with
        | Some a -> Some a
        | None ->
            use con = new SqlConnection(ConnectionString)
            con.Open()
            use cmd = new SqlCommand("select * from Actors where Id = @id", con)
            setParam cmd "@id" id
            use reader = cmd.ExecuteReader()
            let actors = map reader
            if actors.Count() = 1 then
                let a = (Actor (actors |> List.head))
                uow.AddToIdentityMap a
                Some a
            else None
  
    // could be moved to seperate data mapper class
    member __.Create a (mkCmd: string -> SqlCommand) =
        use cmd = mkCmd "insert into Actors (Id, Name, Born) values (@id, @name, @born)"
        setParam cmd "@id" a.Id
        setParam cmd "@name" a.Name
        setParam cmd "@born" a.Born
        if cmd.ExecuteNonQuery() <> 1 then failwith "Unable to insert actor"

    member x.Update a (mkCmd: string -> SqlCommand) =
        let original = uow.GetFromIdentityMap a.Id
        let current = x.GetInternalById a.Id mkCmd
        match current with
        | Some a -> if original <> current then raise(DBConcurrencyException())
        | None -> raise(DBConcurrencyException())

        use cmd = mkCmd "update Actors set Name = @name, Born = @born where Id = @id"
        setParam cmd "@id" a.Id
        setParam cmd "@name" a.Name
        setParam cmd "@born" a.Born
        if cmd.ExecuteNonQuery() <> 1 then failwith "Unable to update actor"

    member __.Delete a (mkCmd: string -> SqlCommand) =
        use cmd = mkCmd "delete from Movies where Id = @id"
        setParam cmd "@id" id
        if cmd.ExecuteNonQuery() <> 1 then failwith "Unable to delete actor"

and MovieRepository(uow) =
    let map (r: SqlDataReader) =       
        seq {
            while r.Read() do
                yield { Id = r.["Id"] :?> Guid
                        Title = r.["Title"] :?> string } } |> Seq.toList

    member private __.GetInternalById id (mkCmd: string -> SqlCommand) =
        use con = new SqlConnection(ConnectionString)
        con.Open()
        use cmd = mkCmd "select * from Movies where Id = @id"
        setParam cmd "@id" id
        use reader = cmd.ExecuteReader()
        let movies = map reader
        if movies.Count() = 1 then Some (Movie (movies|> List.head))
        else None

    member __.GetById id =
        match uow.GetFromIdentityMap id with
        | Some a -> Some a
        | None ->
            use con = new SqlConnection(ConnectionString)
            con.Open()
            use cmd = new SqlCommand("select * from movies where Id = @id", con)
            setParam cmd "@id" id
            use reader = cmd.ExecuteReader()
            let movies = map reader
            if movies.Count() = 1 then
                let a = (Movie (movies |> List.head))
                uow.AddToIdentityMap a
                Some a
            else None

    member __.Create m (mkCmd: string -> SqlCommand) =
        use cmd = mkCmd "insert into Movies (Id, Title) values (@id, @title)"
        setParam cmd "@id" m.Id
        setParam cmd "@title" m.Title
        if cmd.ExecuteNonQuery() <> 1 then failwith "Unable to insert movie"

    member __.Update m (mkCmd: string -> SqlCommand) =
        use cmd = mkCmd "update Movies set Title = @title where Id = @id"
        setParam cmd "@id" m.Id
        setParam cmd "@title" m.Title
        if cmd.ExecuteNonQuery() <> 1 then failwith "Unable to update movie"

    member __.Delete m (mkCmd: string -> SqlCommand) =
        use cmd = mkCmd "delete from Movies where Id = @id"
        setParam cmd "@id" id
        if cmd.ExecuteNonQuery() <> 1 then failwith "Unable to delete movie"

and ActorsMoviesRepository(uow) =
    let map (r: SqlDataReader) =       
        seq {
            while r.Read() do
                yield { Id = r.["Id"] :?> Guid
                        ActorId = r.["ActorId"] :?> Guid 
                        MovieId = r.["MovieId"] :?> Guid } } |> Seq.toList

    member private __.GetInternalById id (mkCmd: string -> SqlCommand) =
        use con = new SqlConnection(ConnectionString)
        con.Open()
        use cmd = mkCmd "select * from ActorsMovies where Id = @id"
        setParam cmd "@id" id
        use reader = cmd.ExecuteReader()
        let actorMovies = map reader
        if actorMovies.Count() = 1 then Some (ActorMovie (actorMovies |> List.head))
        else None

    member __.GetById id =
        match uow.GetFromIdentityMap id with
        | Some a -> Some a
        | None ->
            use con = new SqlConnection(ConnectionString)
            con.Open()
            use cmd = new SqlCommand("select * from ActorsMovies where Id = @id", con)
            setParam cmd "@id" id
            use reader = cmd.ExecuteReader()
            let actorMovies = map reader
            if actorMovies.Count() = 1 then
                let a = (ActorMovie (actorMovies |> List.head))
                uow.AddToIdentityMap a
                Some a
            else None

    member __.GetByActorId id =
        // this method doesn't return a single item by primary key
        // so we can't look for it in the identity map
        use con = new SqlConnection(ConnectionString)
        con.Open()
        use cmd = new SqlCommand("select * from ActorsMovies where ActorId = @id", con)
        setParam cmd "@id" id
        use reader = cmd.ExecuteReader()
        let actorsMovies = map reader

        for am in actorsMovies do
            match uow.GetFromIdentityMap am.Id with
            | Some _ -> ()
            | None -> uow.AddToIdentityMap (ActorMovie am)    
        actorsMovies

    member __.GetByMovieId id =
        // close duplicate of GetByActorId. Any query on non-primary key
        // will look like this except for different SQL query
        use con = new SqlConnection(ConnectionString)
        con.Open()
        use cmd = new SqlCommand("select * from ActorsMovies where MovieId = @id", con)
        setParam cmd "@id" id
        use reader = cmd.ExecuteReader()
        let actorsMovies = map reader

        for am in actorsMovies do
            match uow.GetFromIdentityMap am.Id with
            | Some _ -> ()
            | None -> uow.AddToIdentityMap (ActorMovie am)    
        actorsMovies

    member __.Create am (mkCmd: string -> SqlCommand) =
        use cmd = mkCmd "insert into ActorsMovies (Id, ActorId, MovieId) values (@id, @actorId, @movieId)"
        setParam cmd "@id" am.Id
        setParam cmd "@actorId" am.ActorId
        setParam cmd "@movieId" am.MovieId
        if cmd.ExecuteNonQuery() <> 1 then failwith "Unable to insert actor movie"

    member __.Update am (mkCmd: string -> SqlCommand) =
        use cmd = mkCmd "update ActorsMovies set ActorId = @actorId, MovieId = @movieId where Id = @id"
        setParam cmd "@id" am.Id
        setParam cmd "@actorId" am.ActorId
        setParam cmd "@movieId" am.MovieId
        if cmd.ExecuteNonQuery() <> 1 then failwith "Unable to update actor movie"

    member __.Delete am (mkCmd: string -> SqlCommand) =
        use cmd = mkCmd "delete from ActorsMovies where Id = @id"
        setParam cmd "@id" id
        if cmd.ExecuteNonQuery() <> 1 then failwith "Unable to delete actor movie"

module Program =
    [<EntryPoint>]
    let main args =
        let uow = UnitOfWork()

        // actors
        let stallone = { Id = Guid.NewGuid(); Name = "Sylvester Stallone"; Born = DateTime(1946, 7, 6) }
        uow.Add (Actor stallone)
           
        uow.Update (Actor { stallone with Name = "Sylvester Stallone II" })
        //uow.Delete (Actor stallone)

        // movies
        let rambo = { Id = Guid.NewGuid(); Title = "Rambo" }
        uow.Add (Movie rambo)

        uow.Update (Movie { rambo with Title = "Rambo II" })
        //uow.Delete (Movie rambo)

        // ActorsMovies
        let stalloneRambo = { Id = Guid.NewGuid(); ActorId = stallone.Id; MovieId = rambo.Id }
        uow.Add (ActorMovie stalloneRambo)

        uow.Update (ActorMovie { stalloneRambo with ActorId = stallone.Id })
        //uow.Delete (ActorMovie stalloneRambo)

        uow.Commit()
        0