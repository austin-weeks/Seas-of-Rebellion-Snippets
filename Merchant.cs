using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class Merchant : ShopBase, IShop, IRewardGranter
{
	public event Action<bool> OnSequence;
	public event Action OnShopOpened;
	public event Action OnShopClosed;
	public event Action<RewardSO> OnRewardObtained;
	public event Action OnMapObtained;
	private Cost refreshShopCost = new(100, CurrencyType.Gold);

	[Header("References")]
	[SerializeField] private Camera shopCamera;
	[SerializeField] private GameObject shopUIParent;
	[SerializeField] private Button backButton;
	[SerializeField] private Button refreshShopButton;
	[SerializeField] private Button mapButton;
	[SerializeField] private AudioSource audioSource;
	[SerializeField] private AudioSource woodCreaking;
	[SerializeField] private Transform playerShopPoint;
	[Header("Settings")]
	[SerializeField] bool turnOnLights;
	[SerializeField] List<GameObject> lanterns;
	
	[Header("Item 1")]
	[SerializeField] private ItemHolder itemSlot1;
	[SerializeField] private ItemShopTemplate item1UI;
	
	[Header("Item 2")]
	[SerializeField] private ItemHolder itemSlot2;
	[SerializeField] private ItemShopTemplate item2UI;
	
	[Header("Item 3")]
	[SerializeField] private ItemHolder itemSlot3;
	[SerializeField] private ItemShopTemplate item3UI;
	
	[Header("Item 4")]
	[SerializeField] private ItemHolder itemSlot4;
	[SerializeField] private ItemShopTemplate item4UI;
	
	[Header("Item 5")]
	[SerializeField] private ItemHolder itemSlot5;
	[SerializeField] private ItemShopTemplate item5UI;

	[Header("Purchasable Rewards")]
	[SerializeField] private Heal healReward;
	[SerializeField] private RelicReward hammerReward;
	[SerializeField] private List<RewardSO> rewardsOptionsReferences;
	private List<RewardSO> rewardOptions;
	
	private PlayerCurrencies playerCurrencies;
	private Button lastSelectedButton;

	private void Awake()
	{
		SetPopupParent();
		
		foreach (GameObject lantern in lanterns) lantern.SetActive(turnOnLights);
		
		IRewardGranter.Instance = this;
		IShop.Instance = this;

		buttons = new() { backButton, refreshShopButton, item1UI.itemButton, item2UI.itemButton, item3UI.itemButton, item4UI.itemButton, item5UI.itemButton };
	}
	bool shopSetUp;
	async void SetUpItems()
	{
		if (shopSetUp) return;
		shopSetUp = true;
		
		ResetItemList();
		await SetSpecificItem(itemSlot1, item1UI, healReward);
		await RandomChooseItem(itemSlot2, item2UI);
		await RandomChooseItem(itemSlot3, item3UI);
		await RandomChooseItem(itemSlot4, item4UI);
		await SetSpecificItem(itemSlot5, item5UI, hammerReward);
	}

	private void Start()
	{
		//StartCoroutine(HandleTutorial());
		if (IBoss.Instance != null)
		{
			IBoss.Instance.OnBossStarted += () => IRewardGranter.Instance = null;
		}
		Hide();
		Player.Instance.GetComponent<PlayerInteract>().OnOpenShop += Show;
		playerCurrencies = Player.Instance.GetComponent<PlayerCurrencies>();

		backButton.onClick.AddListener(Hide);
		
		refreshShopButton.onClick.AddListener(RefreshShop);

		GameInput.Instance.OnCancelAction += OnCancelAction;
	}
	
	private async Awaitable SetSpecificItem(ItemHolder itemSlot, ItemShopTemplate itemUI, RewardSO reward)
	{
		reward.Initialize();
		
		itemSlot.rewardSO = reward;
		itemSlot.rewardVisual = Instantiate(reward.rewardVisual, itemSlot.transform);
		
		itemUI.SetUI(reward);
		
		itemSlot.rewardVisual.Poof(true, true);
		await Awaitable.WaitForSecondsAsync(.13f);
		
		itemUI.itemButton.onClick.AddListener(() => PurchaseItem(itemSlot, itemUI));
	}
	private async Awaitable RandomChooseItem(ItemHolder itemSlot, ItemShopTemplate itemUI)
	{
		if (rewardOptions.Count == 0) { print("No shop rewards remaining"); return; }

		int index = UnityEngine.Random.Range(0, rewardOptions.Count);
		RewardSO reward = rewardOptions[index];

		reward.Initialize();
		
		rewardOptions.Remove(reward);
		
		itemSlot.rewardSO = reward;
		itemSlot.rewardVisual = Instantiate(reward.rewardVisual, itemSlot.transform);
		
		itemUI.SetUI(reward);
		
		itemSlot.rewardVisual.Poof(true, true);
		await Awaitable.WaitForSecondsAsync(0.13f);

		itemUI.itemButton.onClick.AddListener(() => PurchaseItem(itemSlot, itemUI));
	}
	
	private void ResetItemList()
	{
		rewardOptions = new();
		foreach (RewardSO reward in rewardsOptionsReferences)
		{
			if (reward.CanUseReward()) rewardOptions.Add(reward);
		}
	}

	private void PurchaseItem(ItemHolder itemSlot, ItemShopTemplate itemUI)
	{
		if (itemSlot.purchased) return;
		RewardSO reward = itemSlot.rewardSO;
		lastSelectedButton = itemUI.itemButton;

		if (playerCurrencies.GetCurrency(reward.cost.currencyType) >= reward.cost.cost)
		{
			SpawnConfirmationPopup(reward.cost, async () =>
			{
				playerCurrencies.SpendCurrency(reward.cost);

				itemSlot.purchased = true;
				itemSlot.rewardSO = null;
				
				itemUI.OnItemPurchase();

				await ClaimReward(reward, itemSlot.rewardVisual);
				RemoveItem(itemSlot, itemUI, false);
			}, itemUI.itemButton);
		}
		else SpawnCantAffordPopup(reward.cost);
	}
	
	private void RefreshShop()
	{
		if (playerCurrencies.GetCurrency(refreshShopCost.currencyType) >= refreshShopCost.cost)
		{
			SpawnConfirmationPopup(refreshShopCost, async () =>
			{
				playerCurrencies.SpendCurrency(refreshShopCost);
				ResetItemList();
				RemoveItem(itemSlot1, item1UI, true);
				await SetSpecificItem(itemSlot1, item1UI, healReward);
				RemoveItem(itemSlot2, item2UI, true);
				await RandomChooseItem(itemSlot2, item2UI);
				RemoveItem(itemSlot3, item3UI, true);
				await RandomChooseItem(itemSlot3, item3UI);
				RemoveItem(itemSlot4, item4UI, true);
				await RandomChooseItem(itemSlot4, item4UI);
			});
		}
		else SpawnCantAffordPopup(refreshShopCost);	
	}
	
	private async void RemoveItem(ItemHolder itemSlot, ItemShopTemplate itemUI, bool instant)
	{
		itemUI.itemButton.onClick.RemoveAllListeners();
		itemSlot.purchased = false;
		
		if (instant && itemSlot.rewardVisual != null)
		{
			itemSlot.rewardVisual.Poof(false, true);
		}
		else if (itemSlot.rewardVisual != null)
		{
			await itemSlot.rewardVisual.OnObtained();
			await Awaitable.WaitForSecondsAsync(1.5f);
		}
		
		itemSlot.rewardVisual = null;
		if (itemSlot.transform.childCount != 0)
			Destroy(itemSlot.transform.GetChild(0).gameObject);
	}
	
	private async Awaitable ClaimReward(RewardSO reward, RewardVisual visual)
	{
		var wait = reward.Implement();
		if (wait != null)
		{
			await wait;
			await Awaitable.WaitForSecondsAsync(0.6f);
		}

		OnRewardObtained?.Invoke(reward);
	}
	
	private bool shopping;
	public bool Shopping()
	{
		return shopping;
	}
	private bool allowCancel;
	private void OnCancelAction()
	{
		if (popupOpen || IPopup.PopupOpen) return;
		if (allowCancel) Hide();
	}
	private async void Show()
	{
		if (shopping) return;
		shopping = true;
		
		OnSequence?.Invoke(true);
		CameraSwapper.SwapCameraSmooth(shopCamera);
		await Awaitable.WaitForSecondsAsync(1.5f);
		
		allowCancel = true;
		
		SetUpItems();
		
		shopUIParent.SetActive(true);
		
		woodCreaking.Play();
		
		//ugly code fix later
		if (LevelManager.Instance.CurrentRegion == Region.Swamp && FindFirstObjectByType<RewardPlinth>() == null)
		{
			LevelEnder.InvokeEvent();
		}
		
		OnShopOpened?.Invoke();
		Player.Instance.ResetPosition(playerShopPoint.position, playerShopPoint.forward);
	}
	private void Hide()
	{
		if (shopping)
		{
			OnSequence?.Invoke(false);
			CameraSwapper.ReturnCameraSmooth();
		}
		shopping = false;
		allowCancel = false;
		
		shopUIParent.SetActive(false);
		woodCreaking.Stop();
		OnShopClosed?.Invoke();
		EventSystem.current.SetSelectedGameObject(null);
		//GameInput.Instance.DisableUIControls();
	}

	public void SelectLastButton()
	{
		if (lastSelectedButton != null)
		{
			if (lastSelectedButton.isActiveAndEnabled) lastSelectedButton.Select();
		}
	}
	
	private void OnDestroy() {
		IShop.Instance = null;
		IRewardGranter.Instance = null;
	}
}
