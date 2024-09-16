using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace File_saving.Models;

public partial class FileSavingDbContext : DbContext
{
    public FileSavingDbContext()
    {
    }

    public FileSavingDbContext(DbContextOptions<FileSavingDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<File> Files { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {

    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<File>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.FileDb).HasColumnName("FileDB");
            entity.Property(e => e.PathFile).HasColumnType("character varying");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
