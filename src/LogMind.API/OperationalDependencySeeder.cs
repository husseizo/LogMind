using LogMind.Core.Models;
using LogMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogMind.API;

/// <summary>
/// Idempotent seeder for OperationalDependency edges.
/// Runs at startup after migrations. Skips seeding if any rows already exist.
/// </summary>
public static class OperationalDependencySeeder
{
    public static async Task SeedAsync(LogMindDbContext db)
    {
        if (await db.OperationalDependencies.AnyAsync()) return;

        var edges = new[]
        {
            // ── Molaslubes Neon downstream dependencies ─────────────────────
            new OperationalDependency
            {
                SourceSystem   = "Molaslubes Neon",
                TargetSystem   = "Odoo",
                DependencyType = "Synchronization",
                Criticality    = "Critical",
                ImpactWeight   = 100,
                Description    = "OdooDeliveryPushJob, OdooInvoicePushJob, and OdooPaymentPushJob feed Odoo ERP state from Neon",
                CreatedAt      = DateTime.UtcNow,
            },
            new OperationalDependency
            {
                SourceSystem   = "Molaslubes Neon",
                TargetSystem   = "Admin Web",
                DependencyType = "ReadModel",
                Criticality    = "High",
                ImpactWeight   = 75,
                Description    = "Admin web application reads invoice, order, and customer data from Neon PostgreSQL",
                CreatedAt      = DateTime.UtcNow,
            },
            new OperationalDependency
            {
                SourceSystem   = "Molaslubes Neon",
                TargetSystem   = "Supervisor Mobile",
                DependencyType = "ReadModel",
                Criticality    = "High",
                ImpactWeight   = 75,
                Description    = "Supervisor mobile app reads Neon for operational dashboards and delivery tracking",
                CreatedAt      = DateTime.UtcNow,
            },
            new OperationalDependency
            {
                SourceSystem   = "Molaslubes Neon",
                TargetSystem   = "Reporting",
                DependencyType = "Reporting",
                Criticality    = "Medium",
                ImpactWeight   = 50,
                Description    = "Business reporting layer reads Neon for invoice, payment, and sales analytics",
                CreatedAt      = DateTime.UtcNow,
            },

            // ── SapOdoo Main downstream dependencies ────────────────────────
            new OperationalDependency
            {
                SourceSystem   = "SapOdoo Main",
                TargetSystem   = "Odoo 19",
                DependencyType = "Writeback",
                Criticality    = "Critical",
                ImpactWeight   = 100,
                Description    = "SapOdoo middleware pushes customers, orders, invoices, and payment refs directly to Odoo 19",
                CreatedAt      = DateTime.UtcNow,
            },
            new OperationalDependency
            {
                SourceSystem   = "SapOdoo Main",
                TargetSystem   = "Molaslubes Neon",
                DependencyType = "DataFlow",
                Criticality    = "Critical",
                ImpactWeight   = 100,
                Description    = "SapOdoo Main feeds the Neon PostgreSQL cache layer; Neon sync jobs depend on this being healthy",
                CreatedAt      = DateTime.UtcNow,
            },

            // ── Sapreplit Autohub downstream dependencies ────────────────────
            new OperationalDependency
            {
                SourceSystem   = "Sapreplit Autohub",
                TargetSystem   = "SAP",
                DependencyType = "Writeback",
                Criticality    = "Critical",
                ImpactWeight   = 100,
                Description    = "Autohub creates customers and sales orders directly in SAP Business One",
                CreatedAt      = DateTime.UtcNow,
            },
            new OperationalDependency
            {
                SourceSystem   = "Sapreplit Autohub",
                TargetSystem   = "productcache.db",
                DependencyType = "ReadModel",
                Criticality    = "High",
                ImpactWeight   = 75,
                Description    = "Autohub frontend reads all products, customers, orders, invoices, and payments from productcache.db",
                CreatedAt      = DateTime.UtcNow,
            },
        };

        db.OperationalDependencies.AddRange(edges);
        await db.SaveChangesAsync();
    }
}
