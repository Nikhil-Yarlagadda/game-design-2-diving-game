using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Records the history of a single game as a serializable report.</summary>
[Serializable]
internal sealed class PlaytestReport
{
    [Serializable]
    internal sealed class Day
    {
        public int number, openingBalance, balance;
        public bool settled;
        public List<Dive> attempts = new();
        public List<Transaction> transactions = new();
    }

    [Serializable]
    internal sealed class Dive
    {
        public string outcome = "In progress";
        public string tank;
        public int machinesPacked, stimulantsPacked;
        public bool camera, flippers;
        public float activeSeconds;
        public int produced, retrieved, salvaged, carriedAtReturn, carriedNow, deposited, lost, credited;
        public int scans, spotsRevealed, machinesDeployed, machinesAbandoned;
        public int deliveriesCollected, deliveriesMissed;
    }

    [Serializable]
    internal sealed class Transaction
    {
        public string kind, item;
        public int amount, samples;
    }

    [Serializable]
    internal sealed class QuestEvent
    {
        public int day;
        public string kind, subject, detail;
    }

    public int schemaVersion = 1;
    public string runId = Guid.NewGuid().ToString("N");
    public string buildVersion = Application.version;
    public int currentDay;
    public bool completed, debuggerUsed;
    public List<Day> days = new();
    public List<QuestEvent> quest = new();
    internal Dive ActiveDive { get; private set; }
    private Day Current => days[days.Count - 1];

    /// <summary>Starts a day with the committed bank balance.</summary>
    internal void StartDay(int number, int balance)
    {
        currentDay = number;
        days.Add(new Day { number = number, openingBalance = balance, balance = balance });
    }

    /// <summary>Starts a new attempt when the player commits a loadout.</summary>
    internal void BeginDive(GameState.DiveLoadout loadout)
    {
        ActiveDive = new Dive
        {
            tank = loadout.Tank, machinesPacked = loadout.Machines,
            stimulantsPacked = loadout.Stimulants, camera = loadout.Camera, flippers = loadout.Flippers
        };
        Current.attempts.Add(ActiveDive);
    }

    /// <summary>Closes the current attempt and captures the final ore and delivery counts.</summary>
    internal void FinishDive(string outcome, int carried, int lost, int abandoned, int missed)
    {
        if (ActiveDive == null) return;
        ActiveDive.outcome = outcome;
        ActiveDive.carriedNow = carried;
        if (outcome is "Returned" or "Incapacitated") ActiveDive.carriedAtReturn = carried;
        ActiveDive.lost = lost;
        ActiveDive.machinesAbandoned = abandoned;
        ActiveDive.deliveriesMissed = missed;
        ActiveDive = null;
    }

    /// <summary>Records a charged shop purchase in the current day's ledger.</summary>
    internal void Purchase(string item, int price, int balance)
    {
        Current.transactions.Add(new Transaction { kind = "Purchase", item = item, amount = -price });
        RefreshBalance(balance);
    }

    /// <summary>Records credits added by the shop's existing credit button.</summary>
    internal void CreditBonus(int amount, int balance)
    {
        Current.transactions.Add(new Transaction { kind = "Credit bonus", amount = amount });
        RefreshBalance(balance);
    }

    /// <summary>Records applied income and expenses and closes the current day's ledger.</summary>
    internal void Settle(bool interior, int samples, int income, int expense, int balance)
    {
        Current.transactions.Add(new Transaction
        {
            kind = interior ? "Cole's mining income" : "Excursion mining income",
            amount = income, samples = samples
        });
        Current.transactions.Add(new Transaction { kind = "Daily expenses", amount = -expense });
        Current.balance = balance;
        Current.settled = true;
        if (!interior && Current.attempts.Count > 0)
            Current.attempts[Current.attempts.Count - 1].credited = samples;
    }

    /// <summary>Records the first occurrence of a quest milestone in this run.</summary>
    internal void Milestone(string kind, string detail = "")
    {
        foreach (QuestEvent entry in quest)
            if (entry.kind == kind) return;
        Choice(kind, detail);
    }

    /// <summary>Records a choice or outcome, including repeated choices on the same day.</summary>
    internal void Choice(string kind, string detail, string subject = "") =>
        quest.Add(new QuestEvent { day = currentDay, kind = kind, subject = subject, detail = detail });

    /// <summary>Marks a run as modified and closes any active attempt before a debug restart.</summary>
    internal void RestartFromDebugger(int day, int balance)
    {
        debuggerUsed = true;
        FinishDive("Interrupted by debugger", ActiveDive?.carriedNow ?? 0, 0, 0, 0);
        StartDay(day, balance);
    }

    /// <summary>Updates the current unfinished day's balance for display and export.</summary>
    internal void RefreshBalance(int balance)
    {
        if (!Current.settled) Current.balance = balance;
    }
}
