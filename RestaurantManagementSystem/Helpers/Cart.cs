using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Helpers
{
    // One line of the cart. Only the dish ID, quantity and kitchen note are stored;
    // names and prices are always read from the database, never from the browser.
    public class CartItem
    {
        public int MenuId { get; set; }
        public int Quantity { get; set; }
        public string? Note { get; set; }
    }

    public record CartSummary(int Count, decimal Subtotal);

    // The cart lives in the session (JSON under SessionKeys.Cart), so it survives page changes,
    // and a guest's cart is kept when they sign in (see SignInUser).
    //   var cart = HttpContext.Session.GetCart();
    //   HttpContext.Session.SetCartQuantity(menuId, 2);
    public static class CartSessionExtensions
    {
        public const int MaxQuantity = 20;      // same limit as the Checkout quantity box

        public static List<CartItem> GetCart(this ISession session)
        {
            var json = session.GetString(SessionKeys.Cart);
            if (string.IsNullOrEmpty(json)) return new List<CartItem>();
            try { return JsonSerializer.Deserialize<List<CartItem>>(json) ?? new List<CartItem>(); }
            catch (JsonException) { return new List<CartItem>(); }
        }

        // Lines with quantity 0 are dropped; an empty cart removes the session key
        public static void SaveCart(this ISession session, List<CartItem> cart)
        {
            var kept = cart.Where(i => i.Quantity > 0).ToList();
            if (kept.Count == 0) session.Remove(SessionKeys.Cart);
            else session.SetString(SessionKeys.Cart, JsonSerializer.Serialize(kept));
        }

        public static void ClearCart(this ISession session) => session.Remove(SessionKeys.Cart);

        // Sets (not adds) the quantity of one dish; 0 removes it. The kitchen note is kept unless updated.
        public static void SetCartQuantity(this ISession session, int menuId, int quantity, string? note = null)
        {
            quantity = Math.Clamp(quantity, 0, MaxQuantity);
            var cart = session.GetCart();
            var line = cart.FirstOrDefault(i => i.MenuId == menuId);
            if (line == null)
            {
                cart.Add(new CartItem { MenuId = menuId, Quantity = quantity, Note = note });
            }
            else
            {
                line.Quantity = quantity;
                if (note != null)
                {
                    line.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
                }
            }
            session.SaveCart(cart);
        }
    }

    public static class CartPricing
    {
        // Is this dish orderable right now? (exists, available, and its category is visible)
        public static IQueryable<RestaurantManagementSystem.Model.Menu> Orderable(ApplicationDbContext db) =>
            db.Menus.Where(m => m.isAvailable && m.MenuCategory != null && m.MenuCategory.categoryActive);

        // Item count + subtotal from database prices. Dishes that are no longer orderable
        // are removed from the cart on the way.
        public static async Task<CartSummary> SummarizeAsync(ApplicationDbContext db, ISession session)
        {
            var cart = session.GetCart();
            if (cart.Count == 0) return new CartSummary(0, 0m);

            var ids = cart.Select(i => i.MenuId).Distinct().ToList();
            var prices = await Orderable(db).AsNoTracking()
                .Where(m => ids.Contains(m.menuID))
                .ToDictionaryAsync(m => m.menuID, m => m.menuPrice);

            var valid = cart.Where(i => prices.ContainsKey(i.MenuId)).ToList();
            if (valid.Count != cart.Count) session.SaveCart(valid);

            return new CartSummary(valid.Sum(i => i.Quantity), valid.Sum(i => prices[i.MenuId] * i.Quantity));
        }
    }
}