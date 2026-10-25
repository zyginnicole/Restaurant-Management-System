using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // ---- One DbSet per table (property name = what you use in queries) 
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

        // ---- Developer 2: Restaurant setup & menu
        public DbSet<Restaurant> Restaurants { get; set; }
        public DbSet<OperatingHour> OperatingHours { get; set; }
        public DbSet<BlockedSlot> BlockedSlots { get; set; }
        public DbSet<DiningArea> DiningAreas { get; set; }
        public DbSet<MenuCategory> MenuCategories { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }

        //Deletion safeguards
        //Restrict = "You can't delete this while something still points to it"
        // handles cascade error or those with special delete rule.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---------- Unique columns ----------
            modelBuilder.Entity<Customer>().HasIndex(c => c.custEmail).IsUnique();
            modelBuilder.Entity<Staff>().HasIndex(s => s.staffEmail).IsUnique();
            modelBuilder.Entity<Admin>().HasIndex(a => a.adminEmail).IsUnique();
            modelBuilder.Entity<MenuCategory>().HasIndex(c => c.categoryName).IsUnique();
            modelBuilder.Entity<DiningArea>().HasIndex(a => a.areaName).IsUnique();

            // ---------- Menu ----------
            modelBuilder.Entity<Menu>()
                .HasOne(m => m.MenuCategory)
                .WithMany(c => c.Menus)
                .HasForeignKey(m => m.categoryID)
                .OnDelete(DeleteBehavior.Restrict);         // can't delete a category that still has items

            // ---------- DiningTable ----------
            modelBuilder.Entity<DiningTable>()
                .HasOne(t => t.DiningArea)
                .WithMany(a => a.DiningTables)
                .HasForeignKey(t => t.areaID)
                .OnDelete(DeleteBehavior.Restrict);         // can't delete an area that still has tables

            // one table number per area (the same number can repeat in a different area)
            modelBuilder.Entity<DiningTable>()
                .HasIndex(t => new { t.areaID, t.tableNumber })
                .IsUnique();

            // ---------- Restaurant -> OperatingHour ----------
            modelBuilder.Entity<OperatingHour>()
                .HasOne(h => h.Restaurant)
                .WithMany(r => r.OperatingHours)
                .HasForeignKey(h => h.restaurantID)
                .OnDelete(DeleteBehavior.Cascade);          // deleting the restaurant removes its hours

            // one row per weekday per restaurant
            modelBuilder.Entity<OperatingHour>()
                .HasIndex(h => new { h.restaurantID, h.dayOfWeek })
                .IsUnique();

            // ---------- Restaurant -> BlockedSlot (slots the admin stopped customers booking) ----------
            modelBuilder.Entity<BlockedSlot>()
                .HasOne(b => b.Restaurant)
                .WithMany(r => r.BlockedSlots)
                .HasForeignKey(b => b.restaurantID)
                .OnDelete(DeleteBehavior.Cascade);

            // a slot can only be blocked once
            modelBuilder.Entity<BlockedSlot>()
                .HasIndex(b => new { b.restaurantID, b.slotStart })
                .IsUnique();

            // ---------- Reservation ----------
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Customer)
                .WithMany(c => c.Reservations)
                .HasForeignKey(r => r.custID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.DiningTable)
                .WithMany(t => t.Reservations)
                .HasForeignKey(r => r.tableID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Staff)
                .WithMany(s => s.Reservations)
                .HasForeignKey(r => r.staffID)
                .OnDelete(DeleteBehavior.SetNull);          // optional FK

            // ---------- Order ----------
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(o => o.custID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Reservation)
                .WithMany(r => r.Orders)
                .HasForeignKey(o => o.reservationID)
                .OnDelete(DeleteBehavior.SetNull);          // optional FK

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Staff)
                .WithMany(s => s.Orders)
                .HasForeignKey(o => o.staffID)
                .OnDelete(DeleteBehavior.SetNull);          // optional FK

            modelBuilder.Entity<Order>()
                .HasOne(o => o.DiningTable)
                .WithMany(t => t.Orders)
                .HasForeignKey(o => o.tableID)
                .OnDelete(DeleteBehavior.Restrict);         // optional FK; keep order history if a table is removed

            // ---------- OrderItem (junction: Order <-> Menu) ----------
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.orderID)
                .OnDelete(DeleteBehavior.Cascade);          // deleting an order removes its lines

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Menu)
                .WithMany(m => m.OrderItems)
                .HasForeignKey(oi => oi.menuID)
                .OnDelete(DeleteBehavior.Restrict);         // keep order history if a menu item is removed

            // ---------- Payment (one-to-one with Order) ----------
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Order)
                .WithOne(o => o.Payment)
                .HasForeignKey<Payment>(p => p.orderID)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------- Feedback ----------
            modelBuilder.Entity<Feedback>()
                .HasOne(f => f.Customer)
                .WithMany(c => c.Feedbacks)
                .HasForeignKey(f => f.custID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Feedback>()
                .HasOne(f => f.Order)
                .WithMany()
                .HasForeignKey(f => f.orderID)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------- Admin-created content ----------
            modelBuilder.Entity<Promotion>()
                .HasOne(p => p.Admin)
                .WithMany(a => a.Promotions)
                .HasForeignKey(p => p.adminID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Announcement>()
                .HasOne(a => a.Admin)
                .WithMany(ad => ad.Announcements)
                .HasForeignKey(a => a.adminID)
                .OnDelete(DeleteBehavior.Restrict);
        }

    }
}