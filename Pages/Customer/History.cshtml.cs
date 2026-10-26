using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Pages.Customer
{
    // History: the signed-in customer's past reservations, orders and payments, and rating a visit.
    // Static UI for now – backend notes:
    //  - OnGet: load for custID (from Session), newest first:
    //      Reservations with status "Completed" or "Cancelled" (+ their Orders and Payment),
    //      Orders with status "Completed" or "Cancelled",
    //      Payments of those orders (Paid, Refunded, Failed).
    //  - "Rate this visit" shows only for a Completed visit whose order has no Feedback row yet
    //    (one rating per visit); a rated visit shows its stars instead.
    //  - OnPostRate (fields: orderId, rating 1–5, comment): save a Feedback row
    //    (custID from Session, feedbackDate = now). The "What stood out?" chips are added to the comment
    //    text, because Feedback has no separate field for them.
    public class HistoryModel : PageModel
    {
        private const int MaxCommentLength = 500;
        private readonly ApplicationDbContext _context;

        public List<ReservationHistoryView> Reservations { get; private set; } = new();
        public List<OrderHistoryView> Orders { get; private set; } = new();
        public List<PaymentHistoryView> Payments { get; private set; } = new();

        public HistoryModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var customerId = CurrentCustomerId();
            if (customerId == null)
            {
                return RedirectToPage("/Customer/Login");
            }

            var completedOrClosed = new[]
            {
                "Completed", "Cancelled", "No-show", "Declined"
            };

            var reservations = await _context.Reservations
                .AsNoTracking()
                .Include(r => r.DiningTable)
                    .ThenInclude(t => t!.DiningArea)
                .Include(r => r.Orders)
                    .ThenInclude(o => o.Payment)
                .Where(r => r.custID == customerId.Value &&
                            completedOrClosed.Contains(r.reservationStatus))
                .OrderByDescending(r => r.reservationDateTime)
                .ToListAsync();

            Reservations = reservations.Select(r => new ReservationHistoryView
            {
                ReservationId = r.reservationID,
                Code = $"R-{r.reservationID:0000}",
                Date = r.reservationDateTime,
                Guests = r.partySize,
                Status = r.reservationStatus,
                TableText = r.DiningTable == null
                    ? string.Empty
                    : $"{r.DiningTable.tableNumber} · {r.DiningTable.DiningArea?.areaName}",
                OrderCodes = string.Join(", ", r.Orders.Select(o => $"ORD-{o.orderID:0000}")),
                Total = r.Orders.Sum(o => o.totalAmount),
                RateOrderId = r.reservationStatus == "Completed"
                    ? r.Orders
                        .Where(o => o.orderStatus == "Completed")
                        .OrderBy(o => o.orderID)
                        .Select(o => (int?)o.orderID)
                        .FirstOrDefault()
                    : null
            }).ToList();

            var orders = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Payment)
                .Include(o => o.DiningTable)
                    .ThenInclude(t => t!.DiningArea)
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Menu)
                .Where(o => o.custID == customerId.Value &&
                            new[] { "Completed", "Cancelled" }.Contains(o.orderStatus))
                .OrderByDescending(o => o.orderDateTime)
                .ToListAsync();

            var feedbackByOrder = await _context.Feedbacks
                .AsNoTracking()
                .Where(f => f.custID == customerId.Value)
                .ToDictionaryAsync(f => f.orderID, f => f.rating);

            Orders = orders.Select(o => new OrderHistoryView
            {
                OrderId = o.orderID,
                Code = $"ORD-{o.orderID:0000}",
                Date = o.orderDateTime,
                Status = o.orderStatus,
                Total = o.totalAmount,
                PaymentStatus = o.Payment?.paymentStatus ?? "None",
                Items = string.Join(", ", o.OrderItems.Select(i =>
                    $"{i.quantity} × {i.Menu?.menuName ?? "Item"}")),
                Rating = feedbackByOrder.GetValueOrDefault(o.orderID)
            }).ToList();

            Payments = await _context.Payments
                .AsNoTracking()
                .Include(p => p.Order)
                .Where(p => p.Order!.custID == customerId.Value)
                .OrderByDescending(p => p.paymentDate)
                .Select(p => new PaymentHistoryView
                {
                    PaymentId = p.paymentID,
                    OrderCode = $"ORD-{p.orderID:0000}",
                    Date = p.paymentDate,
                    Method = p.paymentMethod,
                    Amount = p.paymentAmount,
                    Status = p.paymentStatus
                })
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostRateAsync(
            int orderId,
            int rating,
            string? comment,
            string[]? chips)
        {
            var customerId = CurrentCustomerId();
            if (customerId == null)
            {
                return RedirectToPage("/Customer/Login");
            }

            if (rating is < 1 or > 5)
            {
                TempData["HistoryMessage"] = "Please choose a rating from 1 to 5 stars.";
                return RedirectToPage();
            }

            var order = await _context.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(o =>
                    o.orderID == orderId &&
                    o.custID == customerId.Value);

            if (order == null)
            {
                return NotFound();
            }

            if (order.orderStatus != "Completed")
            {
                TempData["HistoryMessage"] =
                    "Only completed visits can be rated.";
                return RedirectToPage();
            }

            var alreadyRated = await _context.Feedbacks.AnyAsync(f =>
                f.custID == customerId.Value &&
                (f.orderID == orderId ||
                 (order.reservationID.HasValue &&
                  _context.Orders.Any(otherOrder =>
                      otherOrder.orderID == f.orderID &&
                      otherOrder.reservationID == order.reservationID))));

            if (alreadyRated)
            {
                TempData["HistoryMessage"] =
                    "This visit has already been rated.";
                return RedirectToPage();
            }

            var selectedChips = (chips ?? Array.Empty<string>())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Take(10)
                .ToList();

            var typedComment = (comment ?? string.Empty).Trim();
            var chipText = selectedChips.Count == 0
                ? string.Empty
                : $"What stood out: {string.Join(", ", selectedChips)}.";

            var finalComment = string.Join(
                " ",
                new[] { chipText, typedComment }
                    .Where(text => !string.IsNullOrWhiteSpace(text)));

            if (finalComment.Length > MaxCommentLength)
            {
                finalComment = finalComment[..MaxCommentLength];
            }

            _context.Feedbacks.Add(new Feedback
            {
                custID = customerId.Value,
                orderID = orderId,
                rating = rating,
                comment = string.IsNullOrWhiteSpace(finalComment)
                    ? null
                    : finalComment,
                feedbackDate = DateTimeOffset.Now
            });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["HistoryMessage"] =
                    "This visit has already been rated.";
                return RedirectToPage();
            }

            TempData["HistoryMessage"] =
                "Thank you for your feedback.";

            return RedirectToPage();
        }

        private int? CurrentCustomerId()
        {
            return HttpContext.Session.IsSignedInAs(UserRoles.Customer)
                ? HttpContext.Session.GetUserId()
                : null;
        }

        public class ReservationHistoryView
        {
            public int ReservationId { get; set; }
            public string Code { get; set; } = string.Empty;
            public DateTime Date { get; set; }
            public int Guests { get; set; }
            public string Status { get; set; } = string.Empty;
            public string TableText { get; set; } = string.Empty;
            public string OrderCodes { get; set; } = string.Empty;
            public decimal Total { get; set; }
            public int? RateOrderId { get; set; }
        }

        public class OrderHistoryView
        {
            public int OrderId { get; set; }
            public string Code { get; set; } = string.Empty;
            public DateTimeOffset Date { get; set; }
            public string Status { get; set; } = string.Empty;
            public decimal Total { get; set; }
            public string PaymentStatus { get; set; } = string.Empty;
            public string Items { get; set; } = string.Empty;
            public int? Rating { get; set; }
        }

        public class PaymentHistoryView
        {
            public int PaymentId { get; set; }
            public string OrderCode { get; set; } = string.Empty;
            public DateTimeOffset Date { get; set; }
            public string Method { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public string Status { get; set; } = string.Empty;
        }
    }
}
