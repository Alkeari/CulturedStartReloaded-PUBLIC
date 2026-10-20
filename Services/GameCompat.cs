using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CulturedStartReloaded.Services
{
    /// <summary>
    ///     Every place the game's API differs across the versions this mod supports, resolved at
    ///     runtime instead of bound at compile time, so one build runs the whole span.
    ///
    ///     Binding these directly is what forced two incompatible assemblies. A member reference is
    ///     resolved when the JIT compiles the method holding it, before any try inside that method
    ///     runs, so a build made against a newer game does not merely lose the newer feature on an
    ///     older one: the method carrying the reference throws where it is called, and a catch
    ///     inside it never gets the chance to run. The BUTR loader makes that reachable, because
    ///     when no assembly matches the running game it loads the highest-versioned one present
    ///     rather than the highest that fits.
    ///
    ///     Everything here answers a safe default when the API is absent, so a game without a
    ///     feature simply does not have it. Same rule as an absent DLC: skipped, never an error.
    /// </summary>
    public static class GameCompat
    {
        private const string AdvancedStartTypeKey = "StartType";

        // Every lookup below names something the game this assembly compiles against does not have,
        // which is the whole point: absence is the answer HasAdvancedStartOptions reports and the
        // reason those patches are skipped. The Harmony analyzer is right that they are missing and
        // wrong that they are mistakes, so it is silenced here alone and stays live everywhere else,
        // where a misspelled member really would be a defect. Each of these was checked by hand
        // against the metadata of a v1.5.2 install.
#pragma warning disable BHA0001, BHA0003
        private static readonly Type? OptionsManagerType =
            AccessTools.TypeByName("SandBox.AdvancedStartOptions.AdvancedStartOptionsManager");

        private static readonly Type? ListOptionType =
            AccessTools.TypeByName("SandBox.AdvancedStartOptions.ListAdvancedStartOption");

        private static readonly PropertyInfo? AdvancedStartDataProperty =
            AccessTools.Property(typeof(Campaign), "AdvancedStartData");

        private static readonly MethodInfo? GetStartTypeMethod = AccessTools.Method(
            "TaleWorlds.CampaignSystem.Extensions.AdvancedStartOptionsExtensions:GetStartType");
#pragma warning restore BHA0001, BHA0003

        private static readonly Func<CharacterObject, bool> IsMarinerGetter =
            BoolGetter<CharacterObject>("IsMariner");

        private static readonly Func<ItemObject, bool> IsUniqueItemGetter =
            BoolGetter<ItemObject>("IsUniqueItem");

        /// <summary>
        ///     Whether this game has the Advanced Starting Options screen at all. It arrived in
        ///     v1.5.0 and exists only in Sandbox; on older games the mod's own first menu asks the
        ///     same question, so nothing is lost by its absence.
        /// </summary>
        public static bool HasAdvancedStartOptions =>
            AdvancedStartDataProperty != null && GetStartTypeMethod != null;

        /// <summary>
        ///     Whether this troop is trained for a deck. The marker arrived with the naval content
        ///     in v1.3.12, and a game without it has no naval troop trees to draw from, so absence
        ///     answers false the way an absent DLC does.
        /// </summary>
        public static bool IsMariner(CharacterObject? troop) => troop != null && IsMarinerGetter(troop);

        /// <summary>
        ///     Whether the game considers this item one of a kind, which is how a campaign artifact
        ///     is kept out of a generated loadout. The property arrived in v1.3.12; below that
        ///     nothing is unique, so false is the honest answer rather than a lost filter.
        /// </summary>
        public static bool IsUniqueItem(ItemObject? item) => item != null && IsUniqueItemGetter(item);

        /// <summary>
        ///     An open delegate over a public bool property, or a constant false when the game does
        ///     not have it. Bound once as a delegate rather than invoked as a PropertyInfo because
        ///     both callers filter the whole item or troop list, and a reflective call per candidate
        ///     is paid thousands of times per query.
        /// </summary>
        private static Func<T, bool> BoolGetter<T>(string propertyName)
        {
            try
            {
                var getter = AccessTools.PropertyGetter(typeof(T), propertyName);
                if (getter == null)
                {
                    CSLogger.Info($"GameCompat: this game has no {typeof(T).Name}.{propertyName}; it reads as false.");
                    return _ => false;
                }

                return (Func<T, bool>)Delegate.CreateDelegate(typeof(Func<T, bool>), getter);
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"GameCompat: binding {typeof(T).Name}.{propertyName} failed " +
                              $"({ex.GetType().Name}); it reads as false.");
                return _ => false;
            }
        }

        /// <summary>
        ///     The start type the player chose on the game's own screen, or null when there is
        ///     none: an older game, Campaign mode, or a Sandbox game started without opening it.
        /// </summary>
        public static string? AdvancedStartType()
        {
            if (!HasAdvancedStartOptions) return null;

            try
            {
                var campaign = Campaign.Current;
                if (campaign == null) return null;

                var data = AdvancedStartDataProperty!.GetValue(campaign);
                if (data == null) return null;

                var startType = GetStartTypeMethod!.Invoke(null, new[] { data }) as string;
                return string.IsNullOrEmpty(startType) ? null : startType;
            }
            catch (Exception ex)
            {
                CSLogger.Error("GameCompat: reading the chosen start type failed.", ex);
                return null;
            }
        }

        /// <summary>
        ///     Adds this mod's two entries to the game's own start-type list. Answers false when
        ///     the screen does not exist or its list could not be found, so the caller can say so
        ///     once rather than per frame.
        /// </summary>
        public static bool AddStartTypeEntries(object? startOptions, params string[] identifiers)
        {
            if (startOptions == null || ListOptionType == null) return false;

            try
            {
                var getOption = MethodTaking(startOptions.GetType(), "GetOption", typeof(string));
                if (getOption == null)
                {
                    CSLogger.Warn("GameCompat: the start options have no GetOption(string).");
                    return false;
                }

                var list = getOption.Invoke(startOptions, new object[] { AdvancedStartTypeKey });
                if (list == null || !ListOptionType.IsInstanceOfType(list)) return false;

                var conditionType = ListOptionType.GetNestedType("ListItemCondition");
                if (conditionType == null) return false;

                var tupleType = typeof(ValueTuple<,>).MakeGenericType(typeof(string), conditionType);
                var addItem = MethodTaking(ListOptionType, "AddItem", tupleType);
                if (addItem == null)
                {
                    CSLogger.Warn("GameCompat: the start-type list has no AddItem for its own item type.");
                    return false;
                }

                var neverLocked = NeverLockedDelegate(conditionType);
                foreach (var id in identifiers)
                    addItem.Invoke(list, new[] { Activator.CreateInstance(tupleType, id, neverLocked) });

                return true;
            }
            catch (Exception ex)
            {
                CSLogger.Error("GameCompat: adding the start entries failed.", ex);
                return false;
            }
        }

        /// <summary>
        ///     The one method of that name taking exactly that parameter type, found by walking the
        ///     declared methods rather than by asking the binder to match a signature.
        ///
        ///     The binder cannot do it here. Game v1.5.2's start options declare both
        ///     <c>GetOption(string)</c> and <c>GetOption&lt;T&gt;(string)</c>, and asking
        ///     <c>AccessTools.Method</c> for the one taking a string threw AmbiguousMatchException,
        ///     which this mod caught and reported as having no list to add to. The visible cost was
        ///     that the Cultured Start and Start Editor entries never reached the game's own Player
        ///     Start list on v1.5.x. Generic definitions are skipped, so an added overload cannot
        ///     bring the ambiguity back.
        /// </summary>
        private static MethodInfo? MethodTaking(Type owner, string name, Type parameterType)
        {
            foreach (var method in AccessTools.GetDeclaredMethods(owner))
            {
                if (method.Name != name || method.IsGenericMethodDefinition) continue;
                var parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == parameterType)
                    return method;
            }

            return null;
        }

        /// <summary>
        ///     A ListItemCondition that reports the entry is never locked. The delegate type is the
        ///     game's own and its parameters cannot be named at compile time, so the method is
        ///     emitted with the signature the delegate asks for: assign null to the out parameter,
        ///     return false.
        ///
        ///     False is the answer that shows the entry. Despite its name the delegate is asked
        ///     "is this locked", not "is this available": the game's own GetNeverDisabledItem
        ///     returns false, and returning true grayed both entries out like Trader.
        /// </summary>
        private static Delegate NeverLockedDelegate(Type conditionType)
        {
            var invoke = conditionType.GetMethod("Invoke")!;
            var parameters = invoke.GetParameters().Select(p => p.ParameterType).ToArray();

            var method = new DynamicMethod(
                "CSR_NeverLocked", typeof(bool), parameters, typeof(GameCompat).Module, true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Stind_Ref);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);

            return method.CreateDelegate(conditionType);
        }

        /// <summary>
        ///     Applies the patches that only mean anything where the Advanced Starting Options
        ///     exist. Their targets cannot be named in an attribute, because a type that is not
        ///     there stops the whole patch class from loading, so they are resolved and patched by
        ///     hand and skipped in silence on games without them.
        /// </summary>
        public static void PatchAdvancedStartOptions(Harmony harmony, Type patchHost)
        {
            if (OptionsManagerType == null || ListOptionType == null)
            {
                CSLogger.Info("GameCompat: this game has no Advanced Starting Options; those patches are skipped.");
                return;
            }

            Patch(harmony, AccessTools.Method(OptionsManagerType, "CreateCampaignStartOptions"),
                patchHost, "AddModStartEntries", prefix: false);
            Patch(harmony, AccessTools.Method(ListOptionType, "GetListItemName"),
                patchHost, "NameModStartEntry", prefix: false);
            Patch(harmony, AccessTools.Method(ListOptionType, "GetListItemDescription"),
                patchHost, "DescribeModStartEntry", prefix: false);

            // Absent on the game this builds against, which is the point: the patch is skipped
            // there. Suppressed here alone so the analyzer still catches a real typo elsewhere.
#pragma warning disable BHA0003
            var vanillaBehavior = AccessTools.TypeByName(
                "TaleWorlds.CampaignSystem.CampaignBehaviors.CampaignAdvancedStartingPlayerOptionsCampaignBehavior");
#pragma warning restore BHA0003
            Patch(harmony, AccessTools.Method(vanillaBehavior, "OnCharacterCreationIsOver"),
                patchHost, "SuppressVanillaStart", prefix: true);

            PatchNavalStartOptions(harmony, patchHost);
        }

        /// <summary>
        ///     Suppresses War Sails' own starting-options behavior for a start this mod granted.
        ///     The DLC hands out the personal ship without checking the start type, so a mod start
        ///     collected a trade ship nothing had promised.
        ///
        ///     The type is named as a string and never referenced, for two reasons: it is absent
        ///     without the DLC, and a direct reference would put it in this assembly's TypeRef
        ///     table, which must stay identical between the two builds. Absence is the
        ///     ordinary case and is reported once, not as an error.
        /// </summary>
        private static void PatchNavalStartOptions(Harmony harmony, Type patchHost)
        {
#pragma warning disable BHA0003
            var navalBehavior = AccessTools.TypeByName(
                "NavalDLC.CampaignBehaviors.NavalAdvancedStartingPlayerOptionsCampaignBehavior");
#pragma warning restore BHA0003
            if (navalBehavior == null)
            {
                CSLogger.Info("GameCompat: War Sails' starting-options behavior is absent; that patch is skipped.");
                return;
            }

            Patch(harmony, AccessTools.Method(navalBehavior, "OnCharacterCreationIsOver"),
                patchHost, "SuppressNavalStart", prefix: true);
        }

        private static void Patch(Harmony harmony, MethodBase? target, Type host, string method, bool prefix)
        {
            if (target == null)
            {
                CSLogger.Warn($"GameCompat: no target for {method}; that patch is skipped.");
                return;
            }

            try
            {
                var patch = new HarmonyMethod(AccessTools.Method(host, method));
                harmony.Patch(target, prefix: prefix ? patch : null, postfix: prefix ? null : patch);
            }
            catch (Exception ex)
            {
                CSLogger.Error($"GameCompat: patching {target.Name} for {method} failed.", ex);
            }
        }

        /// <summary>
        ///     Subscribes to the end of character creation. v1.5.0 made the event carry an int the
        ///     mod has no use for, so the listener is built to whatever shape this game's event
        ///     takes.
        /// </summary>
        public static void ListenForCharacterCreationOver(object owner, Action handler)
        {
            try
            {
                var mbEvent = AccessTools.Property(typeof(CampaignEvents), "OnCharacterCreationIsOverEvent")
                    ?.GetValue(null);
                if (mbEvent == null)
                {
                    CSLogger.Error("GameCompat: OnCharacterCreationIsOverEvent is missing; the start cannot be applied.");
                    return;
                }

                var add = AccessTools.Method(mbEvent.GetType(), "AddNonSerializedListener");
                if (add == null)
                {
                    CSLogger.Error("GameCompat: the creation event has no AddNonSerializedListener.");
                    return;
                }

                var listenerType = add.GetParameters()[1].ParameterType;
                object listener = listenerType == typeof(Action)
                    ? handler
                    : new Action<int>(_ => handler());

                add.Invoke(mbEvent, new[] { owner, listener });
            }
            catch (Exception ex)
            {
                CSLogger.Error("GameCompat: subscribing to the end of character creation failed.", ex);
            }
        }

        /// <summary>
        ///     Founds the player's kingdom, filling every field the game's own founding
        ///     call accepts rather than the four it needs. The parameter list grew across
        ///     the supported span, from
        ///     (name, informal, culture, founder, policies, text, title, rulerTitle) on
        ///     v1.3.15 through v1.4.8, to that plus a formal name, a string id, a banner
        ///     and four colors on v1.5.0. Arguments are therefore bound BY PARAMETER
        ///     NAME: positional binding silently put the realm's title where the policy
        ///     list belongs, and every field left unbound is a realm that reads as
        ///     something a mod made rather than something the world contains.
        ///     Returns whether the ruler title was bound, since a version without that
        ///     parameter needs it set afterwards instead.
        /// </summary>
        public static bool CreateKingdom(object kingdomManager, TextObject name, TextObject informalName,
            TextObject title, CultureObject culture, Clan founder, TextObject? encyclopediaText,
            TextObject? rulerTitle)
        {
            try
            {
                var method = AccessTools.GetDeclaredMethods(kingdomManager.GetType())
                    .FirstOrDefault(m => m.Name == "CreateKingdom" && !m.IsStatic
                                         && m.GetParameters().Length >= 4
                                         && m.GetParameters()[0].ParameterType == typeof(TextObject));
                if (method == null)
                {
                    CSLogger.Error("GameCompat: no CreateKingdom overload was found.");
                    return false;
                }

                var parameters = method.GetParameters();
                var args = new object?[parameters.Length];
                bool rulerTitleBound = false;
                var bound = new List<string>();

                for (var i = 0; i < parameters.Length; i++)
                {
                    var parameter = parameters[i];
                    object? value = parameter.Name switch
                    {
                        "kingdomName" => name,
                        "informalName" => informalName,
                        "formalName" => title,
                        "encyclopediaTitle" => title,
                        "encyclopediaText" => encyclopediaText,
                        "encyclopediaRulerTitle" => rulerTitle,
                        "culture" => culture,
                        "founderClan" => founder,
                        // Banners and their colors are the banner editor's, not ours
                        _ => null
                    };

                    if (value != null && parameter.ParameterType.IsInstanceOfType(value))
                    {
                        args[i] = value;
                        bound.Add(parameter.Name ?? "?");
                        if (parameter.Name == "encyclopediaRulerTitle") rulerTitleBound = true;
                        continue;
                    }

                    // An unrecognized or mistyped optional keeps the game's own default
                    args[i] = parameter.IsOptional
                        ? Type.Missing
                        : parameter.ParameterType.IsValueType
                            ? Activator.CreateInstance(parameter.ParameterType)
                            : null;
                }

                method.Invoke(kingdomManager, args);
                CSLogger.Info($"GameCompat: founded the kingdom with {bound.Count} of " +
                              $"{parameters.Length} fields bound ({string.Join(", ", bound)}).");
                return rulerTitleBound;
            }
            catch (Exception ex)
            {
                CSLogger.Error("GameCompat: founding the kingdom failed.", ex);
                return false;
            }
        }

        /// <summary>
        ///     Renames a kingdom. v1.5.0 added a required title alongside the name and informal
        ///     name.
        /// </summary>
        /// <summary>
        ///     The ruler title is what the culture calls its monarch. Its setter is
        ///     not public, so it is reached by reflection like the rest of this file;
        ///     a realm without one still reads correctly, so failure is not fatal.
        /// </summary>
        public static void SetRulerTitle(Kingdom kingdom, TextObject title)
        {
            try
            {
                var setter = AccessTools.PropertySetter(typeof(Kingdom), "EncyclopediaRulerTitle");
                if (setter == null)
                {
                    CSLogger.Warn("GameCompat: no EncyclopediaRulerTitle setter was found.");
                    return;
                }

                setter.Invoke(kingdom, new object?[] { title });
            }
            catch (Exception ex)
            {
                CSLogger.Warn($"GameCompat: setting the ruler title failed ({ex.GetType().Name}).");
            }
        }

        public static void ChangeKingdomName(Kingdom kingdom, TextObject name)
        {
            try
            {
                var method = AccessTools.GetDeclaredMethods(typeof(Kingdom))
                    .FirstOrDefault(m => m.Name == "ChangeKingdomName");
                if (method == null)
                {
                    CSLogger.Error("GameCompat: no ChangeKingdomName was found.");
                    return;
                }

                var args = Enumerable.Repeat((object?)name, method.GetParameters().Length).ToArray();
                method.Invoke(kingdom, args);
            }
            catch (Exception ex)
            {
                CSLogger.Error("GameCompat: renaming the kingdom failed.", ex);
            }
        }
    }
}
