# Joint

One place to see a household's money when it lives in separate accounts.

Joint is an Android app for couples and families who keep their own bank and investment
accounts instead of opening a joint one. Each person connects their accounts, and everyone
in the household sees the same picture: what there is, what came in, what went out, and
who spent it.

It only reads. Joint cannot move money, place orders or change anything at a bank.

This is a personal project built for one household. It is not a hosted service and is not
in any app store; the code is here for anyone who wants to read it or run their own copy.

## What it does

**Home.** The household's total balance, each person's share of it, and every account
grouped into Everyday, Savings and Investments. Investment accounts show their holdings and
how much they have gained or lost.

**Transactions.** Everyone's transactions in one list, by day, with who made each one.
Open a payment to give it a category, or to mark its payee as a subscription, a regular
payment such as rent, or a transfer to another account of your own. A mark applies to every
payment to that payee, past and future.

**Insights.** Income against spending month by month, this month's spending compared with
last month's day by day, and who spent what. A second view adds up subscriptions and
regular payments; a third shows a month's spending by category.

**Budget.** One monthly amount for everyday spending. Subscriptions and regular payments
are kept out of it and shown alongside, and the app projects where the month is heading at
the current pace.

**Goals.** Saving and investment goals, measured automatically against the balances of
your savings and investment accounts.

**Households.** Invite someone with a one-time code and you both see the same data. Leave,
and your accounts go with you.

## Where the data comes from

| Source | What it brings |
|---|---|
| [Lunch Flow](https://www.lunchflow.app/) | Balances and transactions from Air Bank and other banks it supports |
| Fio banka | Balances and transactions, straight from Fio |
| Trading 212 | Balances and holdings of Invest and Stocks ISA accounts |

Accounts are read when you connect them and then every six hours.

### Connecting an account

Each person connects their own accounts from the Profile tab.

- **Lunch Flow.** Create a Lunch Flow account and connect your bank there. Add a
  destination of type "REST API", switch on the accounts to share, and paste its API key
  into Joint.
- **Fio.** In Fio internet banking, open Settings, then API, and add a token with the
  "watch the account" right only. Paste it into Joint. A token covers one account, so repeat
  this with a token of its own for each further account. Fio
  hands out the last 90 days of history.
- **Trading 212.** In its app, open Settings, then API, and generate a key with only the
  "Account data" and "Portfolio" permissions. Paste the key and its secret into Joint.

## Privacy and safety

- Every connection uses a read-only key or token. None of them can make a payment or a trade.
- Those keys are encrypted before they are stored, and are never sent back to the app.
- Sign-in is with Google. Joint never sees or stores a password.
- An optional app lock asks for a PIN or a fingerprint every time the app is opened, and
  hides the app's preview in the recent-apps screen.
- A household's data is visible only to its members.

## Good to know

- Android only, installed from an APK.
- Amounts are never converted between currencies. Totals are in the currency your
  household uses most, and other currencies are listed separately or left out.
- Transfers between your own accounts are recognised by a rule (the same amount leaving one
  account and arriving in another within two days), so the odd one is missed or caught by
  mistake. Trading 212 shares no transactions, so money sent there is marked by hand once.
- The in-app guide, under Profile, explains how every figure is worked out.

## Built with

.NET 11 throughout: an ASP.NET Core backend on Postgres, and a .NET MAUI app. Sign-in and
the database are hosted on Supabase.

To run your own copy, see [docs/setup.md](docs/setup.md).

## Licence

[MIT](LICENSE)
