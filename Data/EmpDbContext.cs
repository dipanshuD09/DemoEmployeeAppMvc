using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DemoMvcApplication.Models;
using Microsoft.EntityFrameworkCore;

namespace DemoMvcApplication.Data
{
    public class EmpDbContext : DbContext
    {
        public EmpDbContext(DbContextOptions<EmpDbContext> options): base(options)
        {
            
        }

        public DbSet<Employee> employees { get; set; }
    }
}