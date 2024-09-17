using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

public static class SaveSystem
{
#if UNITY_EDITOR
	private static readonly string SAVE_FOLDER = Application.dataPath + "/Saves/";
#else
	private static readonly string SAVE_FOLDER = Application.persistentDataPath + "/Saves/";
#endif
	
	private const string OPTIONS_DATA_FILE = "/OptionsSaveData.txt";
	private const string LEVEL_PROGRESS_FILE = "/LevelProgress.txt";
	private const string PLAYER_STATS_DATA_FILE = "/PlayerStatsData.txt";
	private const string PRE_RUN_CURRENCIES_FILE = "/PreRunCurrencies.txt";
	private const string PLAYER_GAME_DATA_FILE = "/PlayerGameData.txt";
	private const string PLAYER_EQUIPMENT_FILE = "/PlayerEquipment.txt";
	
	//various bits of data we are saving
	public class SettingsData
	{
		public GameInput.MovementInputMode movementInputMode;
	}

	public class LevelProgress
	{
		public float gameTimer;
		public string currentLevel;
		public int currentLevelProgress;
		public Region currentWorld;
		public RewardType nextReward;
		public List<string> usedLevels;
	}
	
	public class PlayerGameData
	{
		public int currentHealth;
		public int healthBoostAmount;
		public int deathDefianceCharges;
		public List<Effect> activeEffects;
		public List<RelicType> currentRelics;
		public SpecialType currentSpecialAttack;
	}
	
	public class PlayerEquipment
	{
		public List<CannonData> cannonData;
	}


	[Serializable]
	public class PlayerStatsData
	{		
		public ShipType shipType;
		public int goldAmount;
		public int scrapsAmount;
		public int gemAmount;
		
		public ShipStats sloop;
		public ShipStats schooner;
		public ShipStats galleon;
		
		public ShipStatuses shipStatuses;
	}
	[Serializable]
	public class PreRunCurrencies
	{
		public int preRunGold;
		public int preRunScraps;
	}
	
	[Serializable] public class ShipStatuses
	{
		public float sloopRepairPercent = 1f;
		public float schoonerRepairPercent = 1f;
		public float galleonRepairPercent = 1f;
	}
	
	public static void SaveAllData()
	{
		SaveSettingsData();
		SavePlayerStatsData();
		SavePlayerGameData();
		SaveLevelProgress();
		SavePlayerEquipment();
	}

	public static void SavePlayerGameData()
	{
		Initialize();
		
		PlayerGameData playerGameData = new PlayerGameData
		{
			currentHealth = PlayerHealth.Instance.GetHealth(),
			healthBoostAmount = PlayerHealth.Instance.GetHealthBoostAmount(),
			deathDefianceCharges = PlayerHealth.Instance.DeathDefianceCharges,
			activeEffects = Player.Instance.GetComponent<BuffDebuffs>().GetSavableActiveEffects(),
			currentRelics = Player.Instance.GetComponent<BuffDebuffs>().GetSavableRelics(),
			currentSpecialAttack = Player.Instance.GetComponent<SpecialAttack>().GetSavableSpecial(),
		};

		string json = JsonUtility.ToJson(playerGameData, true);

		File.WriteAllText(SAVE_FOLDER + PLAYER_GAME_DATA_FILE, json);
	}
	
	public static void SavePlayerEquipment()
	{
		Initialize();

		PlayerEquipment playerEquipment = new()
		{
			cannonData = Player.Instance.GetComponent<PlayerCannonManager>().GetSavableCannonData(),
		};

		string json = JsonUtility.ToJson(playerEquipment, true);

		File.WriteAllText(SAVE_FOLDER + PLAYER_EQUIPMENT_FILE, json);
	}

	public static void SavePlayerStatsData()
	{
		Initialize();
		PlayerStatsData playerData;
		
		if (!Player.Instance)
		{
			playerData = GetDefaultPlayerShipData();
		}
		else
		{
			PlayerShipStats playerShipStats = PlayerShipStats.Instance;
			PlayerCurrencies playerCurrencies = PlayerCurrencies.Instance;
			playerData = new PlayerStatsData
			{
				shipType = playerShipStats.CurrentShip,
				goldAmount = playerCurrencies.GetPlayerGold(),
				scrapsAmount = playerCurrencies.GetPlayerScraps(),
				gemAmount = playerCurrencies.GetPlayerGems(),
				
				sloop = playerShipStats.Sloop,
				schooner = playerShipStats.Schooner,
				galleon = playerShipStats.Galleon,
				
				shipStatuses = playerShipStats.ShipStatuses,
			};
		}

		string json = JsonUtility.ToJson(playerData, true);

		File.WriteAllText(SAVE_FOLDER + PLAYER_STATS_DATA_FILE, json);

		// ES3.Save(PLAYER_STATS_DATA, playerData);
	}

	public static void SaveLevelProgress()
	{
		Initialize();
	
		RewardType rewardType;
		if (RewardChooser.Instance != null)
		{
			if (RewardChooser.Instance.GetNextReward() != null) rewardType = RewardChooser.Instance.GetNextReward().rewardType;
			else rewardType = RewardType.Repair;
		}
		else rewardType = RewardType.Repair;
		float timePlaying = GameTimer.Instance != null ? GameTimer.Instance.elapsedTime : 0f;
		LevelProgress levelProgress = new()
		{
			gameTimer = timePlaying,
			currentLevel = LevelManager.Instance.GetSavableCurrentLevel(),
			currentLevelProgress = LevelManager.Instance.CurrentLevelProgress,
			currentWorld = LevelManager.Instance.CurrentRegion,
			usedLevels = LevelManager.Instance.GetSavableUsedLevels(),
			nextReward = rewardType,
		};

		string json = JsonUtility.ToJson(levelProgress, true);

		File.WriteAllText(SAVE_FOLDER + LEVEL_PROGRESS_FILE, json);
	}
	public static void SavePreRunCurrencies()
	{
		Initialize();

		PreRunCurrencies preRunCurrencies = new()
		{
			preRunGold = PlayerCurrencies.Instance.GetPlayerGold(),
			preRunScraps = PlayerCurrencies.Instance.GetPlayerScraps(),
		};

		string json = JsonUtility.ToJson(preRunCurrencies, true);

		File.WriteAllText(SAVE_FOLDER + PRE_RUN_CURRENCIES_FILE, json);
	}


	public static void SaveSettingsData()
	{
		Initialize();

		SettingsData optionsData = new SettingsData
		{
			movementInputMode = GameInput.Instance.GetMovementInputMode(),
		};

		string json = JsonUtility.ToJson(optionsData, true);

		File.WriteAllText(SAVE_FOLDER + OPTIONS_DATA_FILE, json);
	}

	#region Loading Methods
	//Generic Load Method to cut down on code.
	public static T LoadSaveData<T>(string savePath, T defaultSaveData)
	{
		string jsonString;
		if (File.Exists(SAVE_FOLDER + savePath))
			jsonString = File.ReadAllText(SAVE_FOLDER + savePath);
		else return defaultSaveData;
		
		if (jsonString != null) return JsonUtility.FromJson<T>(jsonString);
		else return defaultSaveData;
	}
	public static PlayerGameData LoadPlayerGameData()
	{
		string saveString;
		PlayerGameData playerGameData;

		if (File.Exists(SAVE_FOLDER + PLAYER_GAME_DATA_FILE))
		{
			saveString = File.ReadAllText(SAVE_FOLDER + PLAYER_GAME_DATA_FILE);
		}
		else
		{
			return GetDefaultPlayerGameData();
		}

		if (saveString != null)
		{
			playerGameData = JsonUtility.FromJson<PlayerGameData>(saveString);
		}
		else
		{
			Debug.LogError("No save available");
			return null;
		}
		return playerGameData;
	}
	public static PlayerStatsData LoadPlayerShipData()
	{
		string saveString;
		PlayerStatsData playerStatsData;
		
		if (File.Exists(SAVE_FOLDER + PLAYER_STATS_DATA_FILE))
		saveString = File.ReadAllText(SAVE_FOLDER + PLAYER_STATS_DATA_FILE);
		
		else return GetDefaultPlayerShipData();
		
		if (saveString != null)
		playerStatsData = JsonUtility.FromJson<PlayerStatsData>(saveString);
		else return null;
		
		return playerStatsData;
		// if (ES3.KeyExists(PLAYER_STATS_DATA))
		// return ES3.Load<PlayerStatsData>(PLAYER_STATS_DATA);
		
		// else return GetDefaultPlayerShipData();
	}
	public static PreRunCurrencies LoadPreRunCurrencies()
	{
		string saveString;
		PreRunCurrencies preRunCurrencies;

		if (File.Exists(SAVE_FOLDER + PRE_RUN_CURRENCIES_FILE))
			saveString = File.ReadAllText(SAVE_FOLDER + PRE_RUN_CURRENCIES_FILE);

		else return GetDefaultPreRunCurrencies();

		if (saveString != null)
			preRunCurrencies = JsonUtility.FromJson<PreRunCurrencies>(saveString);
		else return null;

		return preRunCurrencies;
		// if (ES3.KeyExists(PLAYER_STATS_DATA))
		// return ES3.Load<PlayerStatsData>(PLAYER_STATS_DATA);

		// else return GetDefaultPlayerShipData();
	}


	public static PlayerEquipment LoadPlayerEquipment()
	{
		string saveString;
		PlayerEquipment playerEquipment;

		if (File.Exists(SAVE_FOLDER + PLAYER_EQUIPMENT_FILE))
		{
			saveString = File.ReadAllText(SAVE_FOLDER + PLAYER_EQUIPMENT_FILE);
		}
		else
		{
			return GetDefaultPlayerEquipmentData();
		}

		if (saveString != null)
		{
			playerEquipment = JsonUtility.FromJson<PlayerEquipment>(saveString);
		}
		else
		{
			Debug.LogError("No PlayerEquipment save available");
			return null;
		}
		return playerEquipment;

	}

	public static LevelProgress LoadLevelProgress()
	{
		string saveString;
		LevelProgress levelProgress;

		if (File.Exists(SAVE_FOLDER + LEVEL_PROGRESS_FILE))
		{
			saveString = File.ReadAllText(SAVE_FOLDER + LEVEL_PROGRESS_FILE);
		}
		else
		{
			return GetDefaultLevelProgress();
		}

		if (saveString != null)
		{
			levelProgress = JsonUtility.FromJson<LevelProgress>(saveString);
		}
		else
		{
			Debug.LogError("No save available");
			return null;
		}
		return levelProgress;
	}

	public static SettingsData LoadOptionsData()
	{
		string saveString;
		SettingsData optionsData;

		if (File.Exists(SAVE_FOLDER + OPTIONS_DATA_FILE))
		{
			saveString = File.ReadAllText(SAVE_FOLDER + OPTIONS_DATA_FILE);
		}
		else
		{
			return GetDefaultOptionsData();
		}

		if (saveString != null)
		{
			optionsData = JsonUtility.FromJson<SettingsData>(saveString);
		}
		else
		{
			Debug.LogError("No save available");
			return null;
		}
		return optionsData;
	}
	#endregion

	private static void Initialize()
	{
		//test for existing Save folder, create if does not exist
		if (!Directory.Exists(SAVE_FOLDER))
		{
			Directory.CreateDirectory(SAVE_FOLDER);
		}
	}

	#region DefaultValues
	private static PlayerGameData GetDefaultPlayerGameData()
	{
		PlayerGameData playerGameData = new PlayerGameData
		{
			currentHealth = 0,
			healthBoostAmount = 0,
			deathDefianceCharges = 0,
			activeEffects = new(),
			currentRelics = new(),
			currentSpecialAttack = SpecialType.Wave
		};
		return playerGameData;
	}
	
	private static PlayerEquipment GetDefaultPlayerEquipmentData()
	{
		Debug.Log("Loading Default Player Equipment");
		PlayerEquipment playerEquipment = new()
		{
			cannonData = new(),
		};
		return playerEquipment;
	}
	private static PreRunCurrencies GetDefaultPreRunCurrencies()
	{
		Debug.Log("Loading Default Pre Run Currencies");
		PreRunCurrencies preRunCurrencies = new()
		{
			preRunGold = 0,
			preRunScraps = 0,
		};
		return preRunCurrencies;
	}

	private static PlayerStatsData GetDefaultPlayerShipData()
	{
		Sails sloopSails = new SailsBasic(){ unlocked = true };
		Sails schoonerSails = new SailsBasic() { unlocked = true };
		Sails galleonSails = new SailsBasic() { unlocked = true };
		Hull sloopHull = new HullBasic() { unlocked = true };
		Hull schoonerHull = new HullBasic() { unlocked = true };
		Hull galleonHull = new HullBasic() { unlocked = true };

		PlayerStatsData defaultSaveValues = new()
		{
			shipType = ShipType.Sloop,
			goldAmount = 0,
			gemAmount = 0,
			scrapsAmount = 0,
			
			//NEED TO REFERENCES SO ASSETS HERE
			sloop = new()
			{
				shipType = ShipType.Sloop,
				shipUnlocked = true,
				currentCannons = ShipStructs.sloop.baseCannons,
				cannonsUnlocked = ShipStructs.sloop.baseCannons,
				baseCannons = ShipStructs.sloop.baseCannons,
				maxCannons = ShipStructs.sloop.maxCannons,
				currentSails = sloopSails,
				unlockedSails = new(){ sloopSails },
				currentHull = sloopHull,
				unlockedHulls = new(){ sloopHull },
			},
			
			schooner = new()
			{
				shipType = ShipType.Schooner,
				shipUnlocked = true,
				currentCannons = ShipStructs.schooner.baseCannons,
				cannonsUnlocked = ShipStructs.schooner.baseCannons,
				baseCannons = ShipStructs.schooner.baseCannons,
				maxCannons = ShipStructs.schooner.maxCannons,
				currentSails = schoonerSails,
				unlockedSails = new(){ schoonerSails },
				currentHull = schoonerHull,
				unlockedHulls = new(){ schoonerHull },
			},
			
			galleon = new()
			{
				shipType = ShipType.Galleon,
				shipUnlocked = true,
				currentCannons = ShipStructs.galleon.baseCannons,
				cannonsUnlocked = ShipStructs.galleon.baseCannons,
				baseCannons = ShipStructs.galleon.baseCannons,
				maxCannons = ShipStructs.galleon.maxCannons,
				currentSails = galleonSails,
				unlockedSails = new(){ galleonSails },
				currentHull = galleonHull,
				unlockedHulls = new(){ galleonHull },
			},
			
			shipStatuses = new(),
		};

		return defaultSaveValues;
	}

	private static LevelProgress GetDefaultLevelProgress()
	{
		LevelProgress defaultLevelProgress = new()
		{
			gameTimer = 0f,
			currentLevel = Loader.HAVANA,
			currentLevelProgress = 0,
			currentWorld = Region.Home,
			nextReward = RewardType.Repair,
			usedLevels = null,
		};

		return defaultLevelProgress;
	}

	private static SettingsData GetDefaultOptionsData()
	{
		SettingsData defaultOptionsData = new()
		{
			movementInputMode = GameInput.Instance.GetMovementInputMode(),
		};

		return defaultOptionsData;
	}
	#endregion
}
