using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Repositories;

public class AlumniPgRepository<T>(AlumniDbContext context) : PgRepository<T, AlumniDbContext>(context), IAlumniPgRepository<T>
    where T : BaseEntity { }
