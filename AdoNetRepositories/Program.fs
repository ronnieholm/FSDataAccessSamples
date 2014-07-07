namespace AdoNetRepositories

// Persistence Patterns
// http://msdn.microsoft.com/en-us/magazine/dd569757.aspx

// The Unit Of Work Pattern And Persistence Ignorance
// http://msdn.microsoft.com/en-us/magazine/dd882510.aspx

open System
open System.Linq
open System.Collections.Generic
open System.Data.SqlClient

// domain
type Actor = 
    { Id: int
      Name: string
      Born: DateTime }

type Movie = 
    { Id: int
      Title: string }

type ActorsMovies =
    { Id: int
      ActorId: int
      MovieId: int }

[<AutoOpen>]
module Extensions =
    let setParam (cmd: SqlCommand) name value =
        cmd.Parameters.AddWithValue(name, value) |> ignore

[<AbstractClass>]
type Repository(con: SqlConnection, trans: SqlTransaction) =
    member __.MkCmd sql = new SqlCommand(sql, con, trans)

// notice how repetitive the task of implementing each repository is
type ActorRepository(con: SqlConnection, trans: SqlTransaction) =
    inherit Repository(con, trans)

    // Fowler's data mapper pattern
    let map (r: SqlDataReader) =
        seq {
            while r.Read() do
                yield { Id = r.["Id"] :?> int
                        Name = r.["Name"] :?> string
                        Born = r.["Born"] :?> DateTime } } |> Seq.toList

    member __.GetById id =
        use cmd = base.MkCmd "select * from Actors where Id = @id"
        setParam cmd "@id" id
        use reader = cmd.ExecuteReader()
        let actors = map reader
        if actors.Count() = 1 then Some(actors |> List.head) else None

    member __.Update (a: Actor) =
        use cmd = base.MkCmd "update Actors set Name = @name, Born = @born where Id = @id"
        setParam cmd "@id" a.Id
        setParam cmd "@name" a.Name
        setParam cmd "@born" a.Born
        let affectedRows = cmd.ExecuteNonQuery()
        if affectedRows > 0 then Some affectedRows else None

    member __.Create a =
        use cmd = base.MkCmd "insert into Actors (Name, Born) values (@name, @born)"
        setParam cmd "@name" a.Name
        setParam cmd "@born" a.Born
        let affectedRows = cmd.ExecuteNonQuery()

        if affectedRows > 0 then
            use cmd1 = base.MkCmd "select @@identity"
            Some(Convert.ToInt32(cmd1.ExecuteScalar()))
        else None

    member __.Delete id =
        use cmd = base.MkCmd "delete from Actors where Id = @id"
        setParam cmd "@id" id
        let affectedRows = cmd.ExecuteNonQuery()
        if affectedRows > 0 then Some affectedRows else None

type MovieRepository(con: SqlConnection, trans: SqlTransaction) =
    inherit Repository(con, trans)

    let map (r: SqlDataReader) =       
        seq {
            while r.Read() do
                yield { Id = r.["Id"] :?> int
                        Title = r.["Title"] :?> string } } |> Seq.toList

    member __.GetById id =
        use cmd = base.MkCmd "select * from Movies where Id = @id"
        setParam cmd "@id" id
        use reader = cmd.ExecuteReader()
        let movies = map reader
        if movies.Count() = 1 then Some(movies |> List.head) else None

    member __.Update (m: Movie) =
        use cmd = base.MkCmd "update Movies set Title = @title where Id = @id"
        setParam cmd "@id" m.Id
        setParam cmd "@title" m.Title
        let affectedRows = cmd.ExecuteNonQuery()
        if affectedRows > 0 then Some affectedRows else None

    member __.Create m =
        use cmd = base.MkCmd "insert into Movies (Title) values (@title)"
        setParam cmd "@title" m.Title
        let affectedRows = cmd.ExecuteNonQuery()

        if affectedRows > 0 then
            use cmd1 = base.MkCmd "select @@identity"
            Some(Convert.ToInt32(cmd1.ExecuteScalar()))
        else None

    member __.Delete id =
        use cmd = base.MkCmd "delete from Movies where Id = @id"
        setParam cmd "@id" id
        let affectedRows = cmd.ExecuteNonQuery()
        if affectedRows > 0 then Some affectedRows else None

type ActorsMoviesRepository(con: SqlConnection, trans: SqlTransaction) =
    inherit Repository(con, trans)

    let map (r: SqlDataReader) =       
        seq {
            while r.Read() do
                yield { Id = r.["Id"] :?> int
                        ActorId = r.["ActorId"] :?> int 
                        MovieId = r.["MovieId"] :?> int } } |> Seq.toList

    member __.GetById id =
        use cmd = base.MkCmd "select * from ActorsMovies where Id = @id"
        setParam cmd "@id" id
        use reader = cmd.ExecuteReader()
        let actorsMovies = map reader
        if actorsMovies.Count() = 1 then Some(actorsMovies |> List.head) else None    

    member __.GetByActorId id =
        use cmd = base.MkCmd "select * from ActorsMovies where ActorId = @id"
        setParam cmd "@id" id
        use reader = cmd.ExecuteReader()
        let actorsMovies = map reader
        if actorsMovies.Count() > 0 then Some actorsMovies else None

    member __.GetByMovieId id =
        use cmd = base.MkCmd "select * from ActorsMovies where MovieId = @id"
        setParam cmd "@id" id
        use reader = cmd.ExecuteReader()
        let actorsMovies = map reader
        if actorsMovies.Count() > 0 then Some actorsMovies else None

    member __.Update am =
        use cmd = base.MkCmd "update ActorsMovies set ActorId = @actorId, MovieId = @movieId where Id = @id"
        setParam cmd "@id" am.Id
        setParam cmd "@actorId" am.ActorId
        setParam cmd "@movieId" am.MovieId
        let affectedRows = cmd.ExecuteNonQuery()
        if affectedRows > 0 then Some affectedRows else None

    member __.Create am =
        use cmd = base.MkCmd "insert into ActorsMovies (ActorId, MovieId) values (@actorId, @movieId)"
        setParam cmd "@actorId" am.ActorId
        setParam cmd "@movieId" am.MovieId
        let affectedRows = cmd.ExecuteNonQuery()

        if affectedRows > 0 then
            use cmd1 = base.MkCmd "select @@identity"
            Some(Convert.ToInt32(cmd1.ExecuteScalar()))
        else None

    member __.Delete id =
        use cmd = base.MkCmd "delete from ActorsMovies where Id = @id"
        setParam cmd "@id" id
        let affectedRows = cmd.ExecuteNonQuery()
        if affectedRows > 0 then Some affectedRows else None

module Program =
    [<EntryPoint>]
    let main args =
        let conString = "Data Source=(localdb)\Projects;Initial Catalog=AdoNetRepositories;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False"
        use con = new SqlConnection(conString)
        con.Open()

        let trans = con.BeginTransaction()
        try
            // actors
            let actors = ActorRepository(con, trans)
            let stallone = { Id = 0; Name = "Sylvester Stallone"; Born = DateTime(1946, 7, 6) }
            let success = actors.Create stallone
            
            let stalloneId =
                match success with
                | Some id -> id
                | None -> failwith "Stallone not created"

            let actor = actors.GetById stalloneId
            let affectedRows =
                match actor with
                | Some a ->                     
                    actors.Update { a with Name = "Sylvester Stallone II" }
                | None -> failwith "Stallone not found"

            //let affectedRow = actors.Delete stalloneId

            // movies
            let movies = MovieRepository(con, trans)
            let rambo = { Id = 0; Title = "Rambo" }
            let success = movies.Create rambo

            let ramboId =
                match success with
                | Some id -> id
                | None -> failwith "Rambo not created"

            let movie = movies.GetById ramboId
            let affectedRows =
                match movie with
                | Some m ->                     
                    movies.Update { m with Title = "Rambo II" }
                | None -> failwith "Rambo not found"

            //let affectedRow = movies.Delete ramboId

            // ActorsMovies
            let actorsMovies = ActorsMoviesRepository(con, trans)
            let stalloneRambo = { Id = 0; ActorId = stalloneId; MovieId = ramboId }
            let success = actorsMovies.Create stalloneRambo

            let tomCruiseTopGunId =
                match success with
                | Some id -> id
                | None -> failwith "Stallone in Rambo not created"

            let actorMovie = actorsMovies.GetById tomCruiseTopGunId
            let affectedRows =
                match actorMovie with
                | Some am ->                     
                    actorsMovies.Update { am with ActorId = stalloneId; MovieId = ramboId }
                | None -> failwith "Top Gun not found"

            let actorsInTopGun = actorsMovies.GetByMovieId ramboId
            let moviesByActor = actorsMovies.GetByActorId stalloneId

            //let affectedRow = actorsMovies.Delete stalloneRambo
            trans.Commit()
        with 
        | e -> 
            trans.Rollback()
            reraise()

        con.Close()
        0