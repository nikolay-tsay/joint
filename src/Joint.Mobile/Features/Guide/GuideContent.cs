namespace Joint.Mobile.Features.Guide;

/// <summary>
/// The text of the in-app guide: how every figure in the app is worked out and where its
/// data comes from. It describes rules that live in the backend, so a change to one of
/// those (InsightsCalculator, BudgetCalculator, GoalRules, PaymentKey, the connectors,
/// the sync schedule) has to be repeated here, or the guide starts telling users something
/// the app no longer does.
/// </summary>
public static class GuideContent
{
    public static readonly IReadOnlyList<GuideSection> Sections =
    [
        new(
            "Where the data comes from",
            "Everything in the app",
            [
                "Joint only reads. It cannot move money, place orders or change anything at your bank.",
                "Air Bank and other banks come through Lunch Flow, using the key of your own Lunch Flow account. "
                    + "Fio comes straight from Fio, using a watch-only token. Trading 212 comes straight from Trading 212.",
                "Accounts are read right after you connect them and then every six hours. Lunch Flow itself "
                    + "refreshes from the bank about once a day, so a payment can show in your bank's app a day or more before it shows here.",
                "Transactions the bank has not booked yet, such as a card payment on hold, are listed at the top of "
                    + "Transactions under Pending, with a Pending badge. They are not counted in any figure (balances aside, "
                    + "which are the bank's own) and cannot be marked or given a category until they are booked. Only "
                    + "banks connected through Lunch Flow report them; Fio shows a payment once it is booked.",
                "Fio hands out the last 90 days of history, so that is where a Fio account starts. A Fio token "
                    + "is for one account, so each Fio account is connected separately with its own token.",
                "Trading 212 gives balances and holdings but no transactions. It appears as up to three accounts: "
                    + "Investments, Main pot and Spending pot. The spending pot is not reported by Trading 212; it is the "
                    + "account total minus the other two, and shows only when something is left over.",
            ]),
        new(
            "Currencies",
            "Everything in the app",
            [
                "Amounts are never converted between currencies.",
                "Wherever one total is shown, it is in the currency your household uses most, and money in other "
                    + "currencies is either listed separately (Home) or left out of that figure (Insights, Budget, Goals).",
            ]),
        new(
            "Balances",
            "Home",
            [
                "The total balance adds up the current balances of every connected account in your main currency. "
                    + "Totals in other currencies are listed on the line below it.",
                "Each person's share is the combined balance of the accounts that person connected, of every type.",
                "The Everyday, Savings and Investments tiles group accounts by their type. You set an account's type "
                    + "by opening it from Home; a new account starts with the type its bank suggests.",
                "The plus or minus figure on the Investments tile is what your holdings are worth now compared with "
                    + "what was paid for them, added up across all holdings. It is the unrealised gain or loss as reported by the broker.",
            ]),
        new(
            "Money moved between your own accounts",
            "Insights and Budget",
            [
                "Moving money between two of your household's accounts is neither income nor spending, so it is left out.",
                "Banks do not mark such transfers, so Joint recognises them by a rule: the same amount leaving one "
                    + "account and arriving in a different one within two days.",
                "In Transactions, both halves of a transfer recognised this way carry the label \"Between your "
                    + "accounts\". A row with that label is already left out, so it needs no mark from you.",
                "This can be wrong both ways. Paying 500 and being paid 500 by someone else the same day is dropped "
                    + "as if it were a transfer. Money sent to Trading 212 has no matching arrival, because no Trading 212 "
                    + "transactions are read, so on its own it counts as spending.",
                "For those, open one such payment in Transactions and mark it \"Own transfer\". Every payment "
                    + "whose name contains the match text shown there is then left out of spending, the budget "
                    + "and the categories, past and future. The text works as it does for subscriptions and you can edit it. "
                    + "Money arriving under the same name still counts as income.",
                "Own transfers are not shown or added up anywhere; the mark only keeps them out of the figures. "
                    + "To undo it, open any payment that carries the \"Own transfer\" label and choose \"Stop tracking\".",
            ]),
        new(
            "Income and spending",
            "Insights, Overview",
            [
                "Overview and Categories show one period, the current calendar month unless you choose another. "
                    + "Use the arrows on the bar above them to step back and forward, or tap its dates to pick the first "
                    + "and the last day, for example from one payday to the day before the next.",
                "A period that runs from a day to the day before the same date a month later (15 October to "
                    + "14 November) steps by whole months, and Joint remembers it: next time it opens on the period that "
                    + "holds today. Any other period steps by its own length in days and is forgotten when the app closes.",
                "Income is every amount arriving in any account in the period; spending is every amount leaving. "
                    + "Transfers between your own accounts are taken out first.",
                "The bars show the chosen period and the five before it. \"Left over\" is income minus spending for each. "
                    + "A red bar means more went out than came in.",
                "\"Spending this period\" (or \"this month\") is a running total by day, drawn against the period "
                    + "before. The sentence above it compares today's total with the total on the same day of that period.",
                "\"Who spent\" adds up what left each person's accounts in the period. It appears when more than one of you has spending.",
                "Days run on UTC time, so a day turns over at 1 or 2 a.m. in Prague.",
                "Recurring always shows calendar months, whatever period is chosen.",
            ]),
        new(
            "Subscriptions and regular payments",
            "Transactions and Insights, Recurring",
            [
                "Nothing is detected automatically. You mark a payment in Transactions as a subscription or a regular "
                    + "payment, and Joint then counts every payment like it, past and future.",
                "\"Like it\" means: the payment's name, the bold line of its row, contains the match text shown when you "
                    + "mark it. Joint suggests that text, leaving out parts that look like a reference number, and you can edit it.",
                "If a payment fits more than one of your match texts, the longest one wins.",
                "The monthly chart adds up the matching payments in each month. \"Last paid\" looks back up to a year.",
                "Only money going out counts. A refund from the same company is not subtracted.",
            ]),
        new(
            "Categories",
            "Transactions and Insights, Categories",
            [
                "Nothing is categorized automatically. You open a payment in Transactions, tap Category and pick one, "
                    + "and Joint gives that category to every payment like it, past and future.",
                "\"Like it\" works the same way as for subscriptions: the payment's name "
                    + "contains the match text shown on the category page, which you can edit. The longest matching text wins.",
                "A category is separate from the subscription and regular payment marks: one payment can have both.",
                "There is a starter list of categories. Typing a new name on the category page adds your own, and it "
                    + "stays in the list for as long as at least one payee uses it.",
                "To delete a category, tap Edit above the list on the category page, then Delete next to it. "
                    + "Every payee in it goes back to having no category and the category leaves the list, a starter one "
                    + "included. Giving a payee that name again brings it back.",
                "The ring on Insights shows the period chosen above it, the same as Overview: each slice is what was "
                    + "spent in a category, and \"Uncategorized\" is spending that no category matches yet. The "
                    + "percentages are shares of that period's spending.",
                "Tapping a category under the ring opens Transactions with only its payments in the ring's period, from "
                    + "everyday accounts. The ring counts every account; the Filter row can widen the list to all accounts.",
                "In Transactions, the Filter row can also keep the list to some days: this month, last month, or a "
                    + "first and last day picked on a month grid.",
                "In Transactions, the Filter row at the top narrows the list to a kind of account and to any number "
                    + "of categories, including payments with no category. A category filter lists payments only. "
                    + "\"Hide transfers between your accounts\" leaves out the rows labelled \"Between your accounts\", both "
                    + "halves, even when the other half is in an account the list is not showing. Payments you marked as an "
                    + "own transfer stay listed.",
                "Only money going out is categorized, and transfers between your own accounts are left out first, "
                    + "as everywhere else.",
            ]),
        new(
            "Budget",
            "Budget",
            [
                "The budget is one amount per calendar month for your household's everyday spending.",
                "Everyday spending is all spending this month, minus what went to your subscriptions and regular "
                    + "payments. Transfers between your own accounts, recognised or marked, are already out.",
                "\"Left to spend\" is the budget minus everyday spending so far.",
                "The amount per day is what is left, divided by the days remaining in the month, today included.",
                "\"Where this month is heading\" takes everyday spending so far, divides it by the days gone, and "
                    + "multiplies by the days in the month. Early in the month a single large purchase moves it a lot.",
                "\"Fixed costs still to come\" are subscriptions and regular payments that were paid last month and "
                    + "have not been paid yet this month. They are shown for information and are not part of the budget.",
                "The budget is not tied to a month: on the 1st the spending starts again from zero against the same "
                    + "amount. Nothing unspent carries over and no overspend is deducted.",
                "A bill counts as everyday spending until you mark it as a regular payment. Money moved to an "
                    + "account of yours that Joint cannot match up counts until you mark it as \"Own transfer\".",
                "Under \"By category\" you can set aside part of the budget for some of your categories, when you make "
                    + "the budget or later. Together "
                    + "they can never be more than the budget; whatever is not given to a category is \"Everything else\".",
                "A category's line counts its everyday spending this month: payments in that category, minus "
                    + "subscriptions and regular payments, the same as the budget as a whole. \"Everything else\" is "
                    + "everyday spending in other categories and in none.",
                "Lowering the budget below what the categories add up to shrinks each category's part in proportion. "
                    + "Removing the budget removes them, and deleting a category removes its part.",
            ]),
        new(
            "Goals",
            "Budget, Goals",
            [
                "A saving goal is measured against the combined balance of every account set to the Savings type. "
                    + "An investment goal is measured against every account set to the Investments type.",
                "The percentage is that balance divided by the goal's target.",
                "All saving goals share the same balance, and so do all investment goals: the money is not divided between goals.",
                "Changing an account's type moves its balance in or out of the goals straight away.",
                "Trading 212's Main pot starts with the Savings type, so it counts towards saving goals unless you change it.",
            ]),
    ];
}
