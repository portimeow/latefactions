using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FactionLanguages;
using FactionLanguages.Utilities;
using RimWorld;
using Verse;

namespace LateFactions
{
	// Rim Languages hands out dialects once, at world generation, and has no UI to assign one later.
	// This fills the gap for factions that appear afterwards, using the mod's own public API so
	// the data stays exactly what the base mod would have written.
	public class LateFactionAllocator : GameComponent
	{
		// Set by the FactionManager.Add hook; handled on the next tick so the faction has its name and def by then.
		internal static bool Pending;

		private static readonly FieldInfo[] DialectFields = typeof(Dialect)
			.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

		public LateFactionAllocator(Game game) { }

		public override void FinalizeInit()
		{
			Pending = false;
			Allocate();
		}

		public override void GameComponentTick()
		{
			if (!Pending)
				return;
			Pending = false;
			Allocate();
		}

		private static void Allocate()
		{
			if (!GameComponent_FactionLanguages.LanguageDatabaseInitialized)
				return;
			GameComponent_FactionLanguages comp = GameComponent_FactionLanguages.CurrentGame;
			RaceLanguageDatabase db = comp?.LanguageDatabase;
			if (db == null)
				return;

			EnsureCache(db);
			Repair(db);

			List<Faction> todo = LanguageUtility.UnallocatedFactions.ToList();
			if (todo.Count == 0)
				return;

			// Parents first: a shorter name can't be a parent of a longer one's child chain otherwise.
			foreach (Faction faction in todo.OrderBy(f => (f.Name ?? "").Length))
			{
				try
				{
					Dialect dialect = FromParentName(faction, db, out string how)
						?? FromSameDef(faction, db, out how)
						?? CreateDialect(faction, comp, db, out how);
					if (dialect == null)
						continue;
					db.LinkFactionDialect(faction, dialect);
					Log.Message($"[LateFactions] {faction.Name} ({faction.def.defName}) -> {dialect.Name} ({how})");
				}
				catch (Exception e)
				{
					Log.Warning($"[LateFactions] could not allocate a dialect for {faction?.Name}: {e}");
				}
			}
		}

		// "Freed Slaves of X", "X Rebels", "X expedition": the longest other faction name contained in ours.
		private static Dialect FromParentName(Faction faction, RaceLanguageDatabase db, out string how)
		{
			how = null;
			string name = faction.Name;
			if (string.IsNullOrEmpty(name))
				return null;
			Faction best = null;
			Dialect bestDialect = null;
			foreach (Faction other in Find.FactionManager.AllFactionsListForReading)
			{
				if (other == faction || string.IsNullOrEmpty(other.Name) || other.Name.Length >= name.Length)
					continue;
				if (name.IndexOf(other.Name, StringComparison.OrdinalIgnoreCase) < 0)
					continue;
				if (best != null && other.Name.Length <= best.Name.Length)
					continue;
				if (db.TryGetLanguage(other, out Dialect d) && d != null)
				{
					best = other;
					bestDialect = d;
				}
			}
			how = best == null ? null : "parent " + best.Name;
			return bestDialect;
		}

		private static Dialect FromSameDef(Faction faction, RaceLanguageDatabase db, out string how)
		{
			how = null;
			foreach (Faction other in Find.FactionManager.AllFactionsListForReading)
			{
				if (other == faction || other.def != faction.def)
					continue;
				if (db.TryGetLanguage(other, out Dialect d) && d != null)
				{
					how = "same def as " + other.Name;
					return d;
				}
			}
			return null;
		}

		private static Dialect CreateDialect(Faction faction, GameComponent_FactionLanguages comp, RaceLanguageDatabase db, out string how)
		{
			how = null;
			if (!LanguageUtility.TryGetMainRaceInFaction(faction, out ThingDef race))
				return null;
			TechLevel tech = faction.def.techLevel;

			List<RaceLanguage> candidates = db.RaceLanguages.Where(l => l.MainRace == race).ToList();
			if (candidates.Count == 0)
			{
				Log.Warning($"[LateFactions] no language exists for race {race.defName}; {faction.Name} left unassigned");
				return null;
			}
			RaceLanguage parent = candidates.FirstOrDefault(l => l.Dialects.Any(d => d.TechLevel == tech)) ?? candidates[0];

			DialectDef def = StandardDef(parent);
			string name = NewName(def, db);

			var dialect = (Dialect)Activator.CreateInstance(typeof(Dialect), true);
			Set(dialect, "id", comp.uniqueIDsManager.GetNextDialectId());
			Set(dialect, "def", def);
			Set(dialect, "name", name);
			Set(dialect, "techLevel", tech);
			Set(dialect, "xpMax", def.GetCost(tech));
			// AddDialect only adds to the list; the faction tab dereferences Dialect.Parent.
			Set(dialect, "parent", parent);
			parent.AddDialect(dialect);
			how = $"new dialect under {parent.Name}";
			return dialect;
		}

		// Human_Ancient's name maker can't generate a name, so new dialects always use the plain standard one.
		private static DialectDef StandardDef(RaceLanguage language)
			=> language.Def == RaceLanguageDefOfLocal.Alien ? DialectDefOfLocal.Alien_Standart : DialectDefOfLocal.Human_Standard;

		private static string NewName(DialectDef def, RaceLanguageDatabase db)
		{
			var used = new HashSet<string>(db.AllDialects.Select(d => d.Name));
			return NameGenerator.GenerateName(def.nameMaker, n => !used.Contains(n));
		}

		// LinkFactionDialect clears this private cache, which is still null on a freshly loaded save.
		private static void EnsureCache(RaceLanguageDatabase db)
		{
			FieldInfo f = typeof(RaceLanguageDatabase).GetField("tmpDialectFactions", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if (f != null && f.GetValue(db) == null)
				f.SetValue(db, Activator.CreateInstance(f.FieldType));
		}

		// An earlier build of this mod saved dialects without a parent language (and some with the unusable
		// Human_Ancient def); those crash the faction tab. A real dialect always has a parent, so null marks ours.
		private static void Repair(RaceLanguageDatabase db)
		{
			foreach (RaceLanguage language in db.RaceLanguages)
			{
				foreach (Dialect dialect in language.Dialects.ToList())
				{
					if (dialect.Parent != null)
						continue;
					Set(dialect, "parent", language);
					if (dialect.Def == null || dialect.Def.defName == "Human_Ancient")
					{
						DialectDef def = StandardDef(language);
						Set(dialect, "def", def);
						Set(dialect, "name", NewName(def, db));
						Set(dialect, "xpMax", def.GetCost(dialect.TechLevel));
					}
					Log.Message($"[LateFactions] repaired dialect {dialect.Name} ({language.Name})");
				}
			}
		}

		private static void Set(Dialect dialect, string field, object value)
		{
			FieldInfo f = DialectFields.First(x => x.Name == field);
			f.SetValue(dialect, value);
		}
	}
}
