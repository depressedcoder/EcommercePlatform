using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.Models;

namespace PaymentService.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _context;
    private readonly ILogger<PaymentRepository> _logger;

    public PaymentRepository(
        PaymentDbContext context,
        ILogger<PaymentRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Payment?> GetByIdAsync(int id)
    {
        return await _context.Payments.FindAsync(id);
    }

    public async Task<Payment?> GetByOrderIdAsync(int orderId)
    {
        return await _context.Payments
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(p => p.OrderId == orderId);
    }

    public async Task<Payment> CreateAsync(Payment payment)
    {
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Created payment record with ID: {PaymentId}", payment.Id);
        return payment;
    }

    public async Task<Payment> UpdateAsync(Payment payment)
    {
        var existingPayment = await _context.Payments.FindAsync(payment.Id);
        if (existingPayment == null)
        {
            throw new KeyNotFoundException($"Payment with ID {payment.Id} not found");
        }

        existingPayment.Status = payment.Status;
        existingPayment.TransactionId = payment.TransactionId;
        existingPayment.Notes = payment.Notes;
        existingPayment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Updated payment record with ID: {PaymentId}", payment.Id);
        return existingPayment;
    }

    public async Task<Payment?> GetByTransactionIdAsync(string transactionId)
    {
        return await _context.Payments.FirstOrDefaultAsync(p => p.TransactionId == transactionId);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var payment = await _context.Payments.FindAsync(id);
        if (payment == null)
        {
            return false;
        }

        _context.Payments.Remove(payment);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Deleted payment record with ID: {PaymentId}", id);
        return true;
    }
} 