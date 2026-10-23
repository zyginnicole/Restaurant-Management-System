using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<Admin> Admins { get; set; }
        public DbSet<Staff> StaffMembers { get; set; }
        public DbSet<DiningTable> DiningTables { get; set; }
        public DbSet<Menu> Menus { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; }
        public DbSet<Promotion> Promotions { get; set; }
        public DbSet<Announcement> Announcements { get; set; }
        public DbSet<NotificationLog> NotificationLogs { get; set; }

        public DbSet<Restaurant> Restaurants { get; set; }
        public DbSet<OperatingHour> OperatingHours { get; set; }
        public DbSet<BlockedSlot> BlockedSlots { get; set; }
        public DbSet<DiningArea> DiningAreas { get; set; }
        public DbSet<MenuCategory> MenuCategories { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Customer>().HasIndex(c => c.custEmail).IsUnique();
            modelBuilder.Entity<Staff>().HasIndex(s => s.staffEmail).IsUnique();
            modelBuilder.Entity<Admin>().HasIndex(a => a.adminEmail).IsUnique();
            modelBuilder.Entity<MenuCategory>().HasIndex(c => c.categoryName).IsUnique();
            modelBuilder.Entity<DiningArea>().HasIndex(a => a.areaName).IsUnique();
            modelBuilder.Entity<DiningArea>().HasIndex(a => a.areaCode).IsUnique();

            modelBuilder.Entity<Menu>()
                .HasOne(m => m.MenuCategory).WithMany(c => c.Menus)
                .HasForeignKey(m => m.categoryID).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DiningTable>()
                .HasOne(t => t.DiningArea).WithMany(a => a.DiningTables)
                .HasForeignKey(t => t.areaID).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<DiningTable>().HasIndex(t => t.tableNumber).IsUnique();

            modelBuilder.Entity<OperatingHour>()
                .HasOne(h => h.Restaurant).WithMany(r => r.OperatingHours)
                .HasForeignKey(h => h.restaurantID).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<OperatingHour>().HasIndex(h => new { h.restaurantID, h.dayOfWeek }).IsUnique();

            modelBuilder.Entity<BlockedSlot>()
                .HasOne(b => b.Restaurant).WithMany(r => r.BlockedSlots)
                .HasForeignKey(b => b.restaurantID).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<BlockedSlot>().HasIndex(b => new { b.restaurantID, b.slotStart }).IsUnique();

            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Customer).WithMany(c => c.Reservations)
                .HasForeignKey(r => r.custID).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.DiningTable).WithMany(t => t.Reservations)
                .HasForeignKey(r => r.tableID).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Staff).WithMany(s => s.Reservations)
                .HasForeignKey(r => r.staffID).OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Customer).WithMany(c => c.Orders)
                .HasForeignKey(o => o.custID).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Reservation).WithMany(r => r.Orders)
                .HasForeignKey(o => o.reservationID).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Staff).WithMany(s => s.Orders)
                .HasForeignKey(o => o.staffID).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Order>()
                .HasOne(o => o.DiningTable).WithMany(t => t.Orders)
                .HasForeignKey(o => o.tableID).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order).WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.orderID).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Menu).WithMany(m => m.OrderItems)
                .HasForeignKey(oi => oi.menuID).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<OrderItem>()
                .HasOne<Promotion>().WithMany()
                .HasForeignKey(oi => oi.promoID).OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Order).WithOne(o => o.Payment)
                .HasForeignKey<Payment>(p => p.orderID).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Feedback>()
                .HasOne(f => f.Customer).WithMany(c => c.Feedbacks)
                .HasForeignKey(f => f.custID).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Feedback>()
                .HasOne(f => f.Order).WithMany()
                .HasForeignKey(f => f.orderID).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Promotion>()
                .HasOne(p => p.Admin).WithMany(a => a.Promotions)
                .HasForeignKey(p => p.adminID).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Announcement>()
                .HasOne(a => a.Admin).WithMany(ad => ad.Announcements)
                .HasForeignKey(a => a.adminID).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
