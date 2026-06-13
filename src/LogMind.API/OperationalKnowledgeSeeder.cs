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

        // ── SAPReplit Autohub entry ───────────────────────────────────────────
        const string autohubTitle = "SAPReplit Autohub Frontend App and ProductCache Synchronization Workflow";

        var autohubExists = await db.OperationalKnowledge
            .AnyAsync(k => k.Title == autohubTitle);

        if (!autohubExists)
        {
            db.OperationalKnowledge.Add(new OperationalKnowledge
            {
                Title    = autohubTitle,
                Category = "Business Process / Integration Architecture",
                System   = "SAPReplit Autohub + SAP + productcache.db",
                Tags     = "SAPReplit,Autohub,Replit,SAP,productcache.db,Products,Customers,Orders,Invoices,Payments,Quartz,SyncDelay,CacheStale,SQLite,OpenInvoice,ClosedInvoice,DeltaSync",

                // Applicable to Sapreplit Autohub ONLY.
                // Do not apply to SapOdoo Main, Molaslubes Neon, or Odoo 19.
                ApplicableSources = """["Sapreplit Autohub"]""",

                Content = """
Scope:
This operational knowledge applies only to logs from:
- Sapreplit Autohub

It must not be applied to:
- SapOdoo Main
- MolasLubes Neon
- Odoo 19 logs

---

SAPReplit Autohub is a frontend Replit application that connects with SAP through cached data and selected direct SAP operations.

SAP remains the main source of truth for:
- Products
- Customers
- Orders
- Invoices
- Payments

SAPReplit Autohub directly creates:
- Customers in SAP
- Orders in SAP

SAPReplit Autohub reads business data from SAP through productcache.db.

The productcache.db contains cached SAP data for:
- Products
- Customers
- Orders
- Invoices
- Payments

Normal workflow:
SAP
→ productcache.db
→ SAPReplit Autohub frontend

Customer creation workflow:
SAPReplit Autohub
→ SAP customer creation
→ SAP returns customer information
→ productcache.db should synchronize the updated customer data

Order creation workflow:
SAPReplit Autohub
→ SAP order creation
→ SAP returns order information
→ productcache.db should synchronize the updated order data

Invoice and payment workflow:
SAP invoices are read through productcache.db.

When a payment is completed:
- The related open invoice in SAP should become closed.
- productcache.db should update quickly.
- SAPReplit Autohub should show the invoice as closed, not open.

Product synchronization workflow:
Products are read from SAP through productcache.db.

When product information changes in SAP:
- productcache.db should update the product data.
- SAPReplit Autohub should display the latest product information.
- Stock, price, item status, and customer/order-related references should not remain stale.

Common issue pattern:
The most common problem in SAPReplit Autohub is delayed synchronization.

Many issues are caused by:
- Quartz jobs running late
- Quartz jobs waiting for each other
- Background sync queue delay
- productcache.db not refreshing fast enough
- Payment status not updating quickly
- Open invoices staying open in cache after payment
- Product changes in SAP not appearing quickly in cache
- Customer or order created in SAP but not visible quickly in the frontend
- Cache data becoming stale
- Multiple sync jobs competing or blocking each other
- Long-running jobs delaying urgent updates

Business impact:
If invoice/payment synchronization is delayed:
- Paid invoices may still appear open in the frontend.
- Finance users may think payment was not completed.
- Customer account status may look incorrect.
- Reporting may show inaccurate unpaid invoice totals.

If product synchronization is delayed:
- Frontend may show old stock, price, or product details.
- Orders may be created using stale product information.
- Users may lose trust in the frontend data.

If customer/order synchronization is delayed:
- Newly created customers or orders may not appear immediately.
- Users may retry creation and accidentally cause duplicate attempts.
- Support teams may think SAP creation failed even when SAP succeeded.

Troubleshooting guidance:
When analyzing Sapreplit Autohub logs, always check:

1. Was the action direct to SAP or from productcache.db?
2. Did SAP complete the operation successfully?
3. Did productcache.db update after SAP changed?
4. Which Quartz job was responsible for the update?
5. Was the Quartz job delayed, blocked, skipped, or still running?
6. Did another sync job hold the database lock?
7. Did SQLite return SQLITE_BUSY or timeout?
8. Did the frontend read stale cache data before sync completed?
9. Is the issue related to product, customer, order, invoice, or payment synchronization?

Important:
For SAPReplit Autohub, many frontend problems are not SAP creation failures. They are often cache synchronization delays.

When a log shows a stale invoice, stale product, missing order, or delayed customer update, first investigate productcache.db synchronization and Quartz job timing before assuming SAP failed.

Recommended improvements:
- Prioritize payment/invoice status sync over slower full cache jobs.
- Use delta sync for invoices, payments, products, customers, and orders.
- Avoid long-running full sync jobs blocking urgent updates.
- Add separate Quartz jobs for high-priority payment updates.
- Add job locking so the same sync does not run twice.
- Add job timeout monitoring.
- Add last successful sync timestamp per entity.
- Add dashboard indicators showing cache freshness.
- Add alerts when invoice/payment cache is stale.
- Add alerts when productcache.db has not updated recently.
- Add retry logic for failed SAP reads.
- Add WAL mode and proper SQLite busy timeout settings.
""",
                IsActive  = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });

            await db.SaveChangesAsync();
        }
    }
}
