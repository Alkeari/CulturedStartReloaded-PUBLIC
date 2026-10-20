using System;
using System.Linq;
using CulturedStartReloaded.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace CulturedStartReloaded.Services.Application.Scenarios
{
    /// <summary>
    ///     Reshapes the diplomacy of the realm the player starts under, on the
    ///     word of the Start Editor or of Cultured Start's realm wars chapter.
    ///
    ///     Whether a realm fights or rests is its monarch's decision. A vassal, a
    ///     landless lord and a mercenary join a realm that already has whatever
    ///     wars the age gave it, so Cultured Start Revamped inherits them and this
    ///     does nothing there however the session was filled in. The editor is full
    ///     control by definition and keeps its Realm's Wars row, and Cultured Start
    ///     has always asked a vassal or a mercenary what those wars are.
    ///
    ///     Null leaves the campaign's own wars standing; an empty set makes peace
    ///     everywhere; otherwise the chosen realms are the exact enemy set, with
    ///     wars declared and peaces made until it matches.
    /// </summary>
    public static class RealmWarSetup
    {
        public static void Apply(StartContext context, Kingdom realm)
        {
            try
            {
                var session = context.Session;

                // The guard is here rather than at the call site because the rule is
                // about who may decide, not about which step runs: a stale composed
                // set surviving in the session must still not move a realm's borders
                if (session.Mode is not (SetupMode.Custom or SetupMode.LifePath))
                {
                    if (session.RealmWars != null)
                        CSLogger.Info(
                            $"RealmWarSetup: a guided start inherits the wars of {realm.Name}; " +
                            "the composed enemy set is ignored.");
                    return;
                }

                var wanted = session.RealmWars;
                if (wanted == null) return;

                foreach (var other in Kingdom.All.Where(k => k != realm && !k.IsEliminated).ToList())
                {
                    bool shouldFight = wanted.Contains(other.StringId);
                    bool fighting = FactionManager.IsAtWarAgainstFaction(realm, other);
                    if (shouldFight == fighting) continue;

                    try
                    {
                        if (shouldFight)
                        {
                            DeclareWarAction.ApplyByDefault(realm, other);
                            CSLogger.Info($"RealmWarSetup: {realm.Name} declared on {other.Name}.");
                        }
                        else
                        {
                            MakePeaceAction.Apply(realm, other);
                            CSLogger.Info($"RealmWarSetup: {realm.Name} made peace with {other.Name}.");
                        }
                    }
                    catch (Exception ex)
                    {
                        context.Report.AddProblem(
                            $"Realm diplomacy change failed for {other.Name}: {ex.GetType().Name}");
                        CSLogger.Error($"RealmWarSetup: change failed for {other.Name}.", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                context.Report.AddProblem($"Realm diplomacy was left as found: {ex.GetType().Name}");
                CSLogger.Error("RealmWarSetup: the realm's wars were left as the campaign had them.", ex);
            }
        }
    }
}
