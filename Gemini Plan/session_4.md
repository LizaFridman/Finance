## Session 4: Shared Expense Logic & Balance Calculator

### Objective
Implement the core financial splitting logic to track shared expenses between you and your partner and compute the exact net balance owed.

### Tasks
1. Add toggle/tagging functionality for transactions where `is_shared = True`.
2. Implement custom split ratios (default 50/50 or custom percentages).
3. Write the calculation module based on the shared expense formula:
   $$\text{Partner Share} = \sum (\text{Shared Expenses Paid by User}) \times \text{Split Ratio}$$
   $$\text{Amount Owed to User} = \text{Partner Share} - \sum (\text{Shared Expenses Paid by Partner})$$
4. Build a ledger view showing individual contributions versus total shared liabilities.

### Prompt for Claude Code (Session 4)
> "Implement the shared expense and balance calculation module. Create functions that calculate total shared expenses, factor in split ratios, and compute the exact net amount the partner owes the user (and vice versa). Ensure transactions can be dynamically flagged as shared or personal via the backend logic."