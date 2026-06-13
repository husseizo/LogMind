using LogMind.Core.Models;
using LogMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogMind.API;

/// <summary>
/// Idempotent seeder for OperationalKnowledge documents.
/// Runs at startup after migrations. Safe to re-run — skips entries that already exist.
/// </summary>
public static class OperationalKnowledgeSeeder
{
    public static async Task SeedAsync(LogMindDbContext db)
    {
        const string entryTitle = "SAP, Odoo 19, MolasLubes Cache and Neon Cache Integration Workflow";

        var exists = await db.OperationalKnowledge
            .AnyAsync(k => k.Title == entryTitle);

        if (exists) return;

        db.OperationalKnowledge.Add(new OperationalKnowledge
        {
            Title    = entryTitle,
            Category = "Business Process / Integration Architecture",
            System   = "SAP + Odoo 19 + MolasLubes Cache + Neon Cache",
            Tags     = "SAP,Odoo19,MolasLubesCache,NeonCache,Products,Customers,Orders,Deliveries,Invoices,Payments,Sync,UDF,CardCode,DocEntry,ORDR",

            // Applicable to SapOdoo Main and Molaslubes Neon logs only.
            // Sapreplit Autohub (formerly "SAP" from C:\SAPLogs) will get its own entry later.
            ApplicableSources = """["SapOdoo Main", "Molaslubes Neon"]""",

            Content = """
Scope:
This operational knowledge applies only to logs from:
- SapOdoo Main (SAP-Odoo integration middleware)
- Molaslubes Neon (MolasLubes cache and Neon sync service)

It does not apply to Sapreplit Autohub logs. Sapreplit Autohub operational knowledge will be added separately.

---

SAP is the main source of truth for core business data.

SAP contains and manages:
- Products
- Customers
- Sales Orders
- Deliveries
- Invoices
- Payments

Odoo 19 receives product data from SAP through an intermediate cache flow:

SAP → MolasLubes Cache → Neon Cache → Odoo 19

Odoo 19 creates Sales Orders and sends them directly to SAP.

When Odoo 19 creates an order:
- Odoo creates the order in Odoo.
- Odoo sends the order directly to SAP.
- SAP creates the Sales Order.
- Odoo writes its Odoo Order Number into a SAP UDF field.
- SAP returns the ORDR DocEntry back to Odoo.
- Odoo stores the SAP ORDR DocEntry for tracking and synchronization.

Odoo 19 also creates Pick List Orders after an order is created.

When Odoo 19 creates a customer:
- Odoo creates the customer.
- Odoo sends the customer directly to SAP.
- SAP creates the Business Partner / Customer.
- Odoo sends its Odoo Customer Number to SAP.
- SAP writes back the SAP CardCode to Odoo.
- Odoo stores the SAP CardCode for future order, invoice, and payment operations.

Invoice creation is initiated from Odoo 19 and sent directly to SAP.

Invoice workflow:
Odoo Order → SAP Sales Order → Delivery / Pick List process → SAP Invoice → Payment

When Odoo creates an invoice:
- Odoo sends invoice creation request to SAP.
- SAP creates the invoice.
- The invoice remains open while waiting for payment.
- Payment is performed from Odoo.
- Odoo writes the payment reference / payment number.
- SAP writes back payment confirmation or SAP payment reference to Odoo.

Default normal process:
After integration is working correctly, the standard business workflow continues as normal:
Sales Order → Delivery → Invoice → Payment

Invoice creation requires:
- Valid SAP customer
- Valid SAP CardCode linked back to Odoo
- Valid Odoo Order Number written to SAP UDF
- Valid SAP ORDR DocEntry written back to Odoo
- Open delivery or valid delivery process
- Available inventory
- Valid warehouse
- Active customer
- Correct item codes
- Correct quantity
- Successful synchronization between SAP and Odoo

Common blockers:
- Negative inventory
- Cancelled delivery
- Missing SAP CardCode in Odoo
- Missing Odoo Order Number in SAP UDF
- Missing SAP ORDR DocEntry in Odoo
- Invalid warehouse
- Warehouse mismatch
- Product not synced from SAP to Odoo
- Product exists in SAP but not in MolasLubes Cache
- Product exists in MolasLubes Cache but not in Neon Cache
- Product exists in Neon Cache but not in Odoo
- Customer created in Odoo but failed in SAP
- SAP customer created but CardCode not written back to Odoo
- Invoice created but payment not completed
- Payment completed in Odoo but not written back to SAP
- SAP API failure
- Odoo API failure
- Cache database unavailable
- Sync job failure
- Duplicate order attempt
- Duplicate invoice attempt
- Duplicate payment reference

Business impact:
If product sync fails:
- Odoo may show stale or missing products.
- Orders may fail because item codes do not exist or are outdated.

If customer sync fails:
- Orders or invoices may fail because Odoo does not have the correct SAP CardCode.

If order sync fails:
- SAP may not receive the order.
- Odoo may not receive SAP ORDR DocEntry.
- Later delivery, invoice, and payment steps may fail.

If invoice creation fails:
- Customer order may remain uninvoiced.
- Payment reconciliation may be blocked.
- Sales reporting may become inaccurate.

If payment writeback fails:
- Invoice may remain open in SAP.
- Odoo may show payment completed while SAP still shows unpaid.
- Finance reconciliation may be incorrect.

Troubleshooting guidance:
When analyzing SapOdoo Main or Molaslubes Neon logs, always check where the failure happened in the chain:

1. Product sync:
SAP → MolasLubes Cache → Neon Cache → Odoo 19

2. Customer creation:
Odoo 19 → SAP → SAP CardCode writeback → Odoo 19

3. Order creation:
Odoo 19 → SAP ORDR → Odoo Order Number UDF in SAP → SAP DocEntry writeback to Odoo

4. Delivery / Pick List:
Odoo 19 order → Pick List creation → SAP delivery process

5. Invoice creation:
Odoo 19 → SAP invoice → invoice remains open pending payment

6. Payment:
Odoo 19 payment → SAP payment writeback → invoice closure / reconciliation

When a log shows a failure, determine:
- Which system produced the error.
- Which workflow step was active.
- Whether the issue is upstream or downstream.
- Whether SAP has the required data.
- Whether Odoo has the required SAP reference.
- Whether cache data is stale or missing.
- Whether the error affects products, customers, orders, deliveries, invoices, or payments.

Important:
Do not treat SAP and Odoo errors separately when logs indicate synchronization or writeback issues. Many failures are caused by missing references between systems.
""",
            IsActive  = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
    }
}
