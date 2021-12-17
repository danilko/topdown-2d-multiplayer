using Godot;
using System;
using System.Collections.Generic;

public class InventoryManager : Node
{
	private InventoryDatabase _inventoryDatabase;

	private GameWorld _gameWorld;

	public override void _Ready()
	{
		_inventoryDatabase = (InventoryDatabase)GetNode("InventoryDatabase");
	}

	public void Initialize(GameWorld gameWorld)
	{
		_gameWorld = gameWorld;
	}

	public List<ItemResource> GetPurchasableItems()
	{
		return _inventoryDatabase.GetItems();
	}

	public ItemResource GetPurchasableItemByID(String itemID)
	{
		return _inventoryDatabase.GetItemByID(itemID);
	}

	// Add item through unit cost in team
	public void PurchaseItem(ItemResource itemResource, Inventory inventory)
	{
		if (inventory != null && itemResource != null && IsInstanceValid(inventory) && IsInstanceValid(itemResource))
		{
			String info = itemResource.ItemID + ";" + (int)inventory.GetAgent().GetTeam() + ";" + inventory.GetAgent().GetUnitID();

			if (GetTree().NetworkPeer != null && !GetTree().IsNetworkServer())
			{
				RpcId(1, nameof(_serverPurchaseItem), info);
			}
			else
			{
				_serverPurchaseItem(info);
			}
		}
	}

	[Remote]
	private void _serverPurchaseItem(String info)
	{
		String[] splitInfo = info.Split(";");
		int infoIndex = 0;

		ItemResource itemResource = _inventoryDatabase.GetItemByID(splitInfo[infoIndex]);
		infoIndex++;

		TeamMapAI teamMapAI = _gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[int.Parse(splitInfo[infoIndex])];
		infoIndex++;

		Agent agent = teamMapAI.GetUnit(splitInfo[infoIndex]);
		infoIndex++;

		if (agent != null && IsInstanceValid(agent))
		{
			Inventory inventory = agent.GetInventory();

			// No need to sync cost as cost will sync by team AI system
			if (inventory != null && IsInstanceValid(inventory) && itemResource != null && IsInstanceValid(itemResource))
			{
				if (inventory.GetAvailableCapacity() > 0 && teamMapAI.ChargeAmount(itemResource.Price))
				{
					inventory.AddItem(itemResource);
				}

				// Sync the client no matter if is passing the lastcheck
				// If not passing, as long as the agent still exit it may indicate cliet is out of sync with server inventory so need to sync
				if (GetTree().NetworkPeer != null && GetTree().IsNetworkServer())
				{
					SyncInventory(-1, agent);
				}
			}

		}
	}

	// Sell item to increase unit amount in team
	public void SellItem(int inventoryIndex, Inventory inventory)
	{
		if (inventory != null && IsInstanceValid(inventory))
		{
			String info = inventoryIndex + ";" + (int)inventory.GetAgent().GetTeam() + ";" + inventory.GetAgent().GetUnitID();

			if (GetTree().NetworkPeer != null && !GetTree().IsNetworkServer())
			{
				RpcId(1, nameof(_serverSellItem), info);
			}
			else
			{
				_serverSellItem(info);
			}
		}
	}


	[Remote]
	private void _serverSellItem(String info)
	{
		String[] splitInfo = info.Split(";");
		int infoIndex = 0;

		int inventoryIndex = int.Parse(splitInfo[infoIndex]);
		infoIndex++;

		TeamMapAI teamMapAI = _gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[int.Parse(splitInfo[infoIndex])];
		infoIndex++;

		Agent agent = teamMapAI.GetUnit(splitInfo[infoIndex]);
		infoIndex++;

		if (agent != null && IsInstanceValid(agent))
		{
			Inventory inventory = agent.GetInventory();

			if (inventory != null && IsInstanceValid(inventory))
			{
				if (inventory.GetItems()[inventoryIndex] != null && !inventory.IsItemIndexInUsed(inventoryIndex))
				{
					ItemResource itemResource = inventory.GetItem(inventoryIndex);
					// Debit back the amount
					teamMapAI.ChargeAmount(-itemResource.Price);

					inventory.RemoveItem(inventoryIndex);
				}

				// Sync the client no matter if is passing (if not passing, it may indicate cliet is out of sync with server inventory so need to sync)
				if (GetTree().NetworkPeer != null && GetTree().IsNetworkServer())
				{
					SyncInventory(-1, agent);
				}
			}
		}
	}
	public void AddItem(ItemResource itemResource, Inventory inventory)
	{
		// Add item only process at master
		if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
		{
			if (inventory != null && itemResource != null && IsInstanceValid(inventory) && IsInstanceValid(itemResource))
			{
				String info = itemResource.ItemID + ";" + (int)inventory.GetAgent().GetTeam() + ";" + inventory.GetAgent().GetUnitID();

				_serverAddItem(info);
			}
		}
	}


	[Remote]
	private void _serverAddItem(String info)
	{
		String[] splitInfo = info.Split(";");
		int infoIndex = 0;

		ItemResource itemResource = _inventoryDatabase.GetItemByID(splitInfo[infoIndex]);
		infoIndex++;

		TeamMapAI teamMapAI = _gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[int.Parse(splitInfo[infoIndex])];
		infoIndex++;

		Agent agent = teamMapAI.GetUnit(splitInfo[infoIndex]);
		infoIndex++;

		if (agent != null && IsInstanceValid(agent) && itemResource != null && IsInstanceValid(itemResource))
		{
			Inventory inventory = agent.GetInventory();

			if (inventory != null && IsInstanceValid(inventory))
			{
				inventory.AddItem(itemResource);

				// Sync the client no matter if is passing (if not passing, it may indicate cliet is out of sync with server inventory so need to sync)
				if (GetTree().NetworkPeer != null && GetTree().IsNetworkServer())
				{
					SyncInventory(-1, agent);
				}
			}
		}
	}



	[Remote]
	private void _serverRemoveItem(String info)
	{
		String[] splitInfo = info.Split(";");
		int infoIndex = 0;

		int inventoryIndex = int.Parse(splitInfo[infoIndex]);
		infoIndex++;

		TeamMapAI teamMapAI = _gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[int.Parse(splitInfo[infoIndex])];
		infoIndex++;

		Agent agent = teamMapAI.GetUnit(splitInfo[infoIndex]);
		infoIndex++;

		int dropItem = int.Parse(splitInfo[infoIndex]);
		infoIndex++;

		if (agent != null && IsInstanceValid(agent))
		{
			Inventory inventory = agent.GetInventory();

			if (inventory != null && IsInstanceValid(inventory))
			{
				if (inventory.GetItem(inventoryIndex) != null)
				{
					ItemResource itemResource = inventory.GetItem(inventoryIndex);
					inventory.RemoveItem(inventoryIndex);

					// Will drop item even if local client not able to remove it, as to sync up state with other servers
					if (dropItem == 1)
					{
						Vector2 itemPosition = agent.GlobalPosition + new Vector2(200f, 200f);

						if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
						{
							String dropItemInfo = itemResource.ItemID + ";" + itemPosition.x + ";" + itemPosition.y + ";";

							// Call locally for server
							_clientCreatePickUp(dropItemInfo);

							if (GetTree().NetworkPeer != null)
							{
								Rpc(nameof(_clientCreatePickUp), dropItemInfo);
							}
						}

					}
				}

				// Sync the client no matter if is passing (if not passing, it may indicate cliet is out of sync with server inventory so need to sync)
				if (GetTree().NetworkPeer != null && GetTree().IsNetworkServer())
				{
					SyncInventory(-1, agent);
				}
			}
		}
	}

	private void _createPickUp(ItemResource itemResource, Vector2 itemPosition)
	{
		Pickup pickup = (Pickup)((PackedScene)GD.Load("res://items/Pickup.tscn")).Instance();
		_gameWorld.AddChild(pickup);

		pickup.Initialize(this, itemResource);
		pickup.GlobalPosition = itemPosition;
	}

	[Remote]
	private void _clientCreatePickUp(String info)
	{
		if (!GetTree().IsNetworkServer())
		{
			String[] splitInfo = info.Split(";");
			int infoIndex = 0;

			ItemResource itemResource = _inventoryDatabase.GetItemByID(splitInfo[infoIndex]);
			infoIndex++;

			float positionX = float.Parse(splitInfo[infoIndex]);
			infoIndex++;

			float positionY = float.Parse(splitInfo[infoIndex]);
			infoIndex++;

			_createPickUp(itemResource, new Vector2(positionX, positionY));
		}
	}

	public void RemoveItem(int inventoryIndex, Inventory inventory, bool dropItem)
	{
		if (inventory != null && IsInstanceValid(inventory))
		{
			String info = inventoryIndex + ";" + (int)inventory.GetAgent().GetTeam() + ";" + inventory.GetAgent().GetUnitID() + ";" + (dropItem ? 1 : 0);

			if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
			{
				_serverRemoveItem(info);

			}
			else
			{
				RpcId(1, nameof(_serverRemoveItem), info);
			}


		}
	}


	public void EquipItem(Inventory inventory, int inventoryItemIndex, Weapon.WeaponOrder weaponOrder, int weaponIndex)
	{
		if (inventory != null && IsInstanceValid(inventory))
		{
			String info = inventoryItemIndex + ";" + (int)inventory.GetAgent().GetTeam() + ";" + inventory.GetAgent().GetUnitID() + ";" + (int)weaponOrder + ";" + weaponIndex;

			if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
			{
				_serverEquipItem(info);
			}
			else
			{
				RpcId(1, nameof(_serverEquipItem), info);
			}
		}
	}

	[Remote]
	private void _serverEquipItem(String info)
	{
		int inventoryItemIndex = int.Parse(info.Split(";")[0]);
		TeamMapAI teamMapAI = _gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[int.Parse(info.Split(";")[1])];
		Agent agent = teamMapAI.GetUnit(info.Split(";")[2]);
		Weapon.WeaponOrder weaponOrder = (Weapon.WeaponOrder)int.Parse(info.Split(";")[3]);
		int weaponIndex = int.Parse(info.Split(";")[4]);
		if (agent != null && IsInstanceValid(agent))
		{
			Inventory inventory = agent.GetInventory();

			// Inventory is not null
			if (inventory != null && IsInstanceValid(inventory) && inventory.GetItem(inventoryItemIndex) != null)
			{
				// It is not in used and is equiptable
				if (!inventory.IsItemIndexInUsed(inventoryItemIndex) && inventory.GetItem(inventoryItemIndex).CurrentItemType == ItemResource.ItemType.EQUIPABLE)
				{
					// Server local directly process
					inventory.EquipItem(inventoryItemIndex, weaponOrder, weaponIndex);
				}

				// Sync the client no matter if is passing (if not passing, it may indicate cliet is out of sync with server inventory so need to sync)
				if (GetTree().NetworkPeer != null && GetTree().IsNetworkServer())
				{
					SyncInventory(-1, agent);
				}
			}
		}
	}

	public void UnequipItem(Inventory inventory, Weapon.WeaponOrder weaponOrder, int weaponIndex, int dropWeapon)
	{
		if (inventory != null && IsInstanceValid(inventory))
		{
			String info = (int)inventory.GetAgent().GetTeam() + ";" + inventory.GetAgent().GetUnitID() + ";" + (int)weaponOrder + ";" + weaponIndex + ";" + dropWeapon;

			if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
			{
				_serverUnequipItem(info);
			}
			else
			{
				RpcId(1, nameof(_serverUnequipItem), info);
			}
		}
	}

	[Remote]
	private void _serverUnequipItem(String info)
	{
		String[] splitInfo = info.Split(";");
		int infoIndex = 0;

		TeamMapAI teamMapAI = _gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[int.Parse(splitInfo[infoIndex])];
		infoIndex++;

		Agent agent = teamMapAI.GetUnit(splitInfo[infoIndex]);
		infoIndex++;

		Weapon.WeaponOrder weaponOrder = (Weapon.WeaponOrder)int.Parse(splitInfo[infoIndex]);
		infoIndex++;

		int weaponIndex = int.Parse(splitInfo[infoIndex]);
		infoIndex++;

		int dropWeapon = int.Parse(splitInfo[infoIndex]);

		if (agent != null && IsInstanceValid(agent))
		{
			Inventory inventory = agent.GetInventory();
			if (inventory != null && IsInstanceValid(inventory))
			{
				if (agent.GetWeapons(weaponOrder)[weaponIndex] != null)
				{
					inventory.UnequipItem(weaponOrder, weaponIndex, dropWeapon);
				}
				// Sync the client no matter if is passing (if not passing, it may indicate cliet is out of sync with server inventory so need to sync)
				if (GetTree().NetworkPeer != null && GetTree().IsNetworkServer())
				{
					SyncInventory(-1, agent);
				}
			}
		}
	}

	public void SyncInventory(int rpcId, Agent agent)
	{
		if (agent != null && IsInstanceValid(agent))
		{
			String info = (int)agent.GetTeam() + "," + agent.GetUnitID() + "," + agent.GetInventory().GetInventoryState();

			if (rpcId != -1)
			{
				RpcId(rpcId, nameof(_syncInventory), info);
			}
			else
			{
				Rpc(nameof(_syncInventory), info);
			}
		}
	}

	[Remote]
	private void _syncInventory(String info)
	{
		// No need to do on server as server already perform it
		if (!GetTree().IsNetworkServer())
		{
			String[] splitInfo = info.Split(",");
			int infoIndex = 0;

			TeamMapAI teamMapAI = _gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[int.Parse(splitInfo[infoIndex])];
			infoIndex++;

			Agent agent = teamMapAI.GetUnit(splitInfo[infoIndex]);
			infoIndex++;

			if (agent != null && IsInstanceValid(agent))
			{
				Inventory inventory = agent.GetInventory();

				if (inventory != null && IsInstanceValid(inventory))
				{
					inventory.SyncInventoryState(splitInfo[infoIndex], _inventoryDatabase);
				}
			}
		}
	}


	public void UseItem(int inventoryIndex, Inventory inventory)
	{
		if (inventory != null && IsInstanceValid(inventory))
		{
			String info = inventoryIndex + ";" + (int)inventory.GetAgent().GetTeam() + ";" + inventory.GetAgent().GetUnitID();

			if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
			{
				_serverUseItem(info);
			}
			else
			{
				RpcId(1, nameof(_serverUseItem), info);
			}

		}
	}


	[Remote]
	private void _serverUseItem(String info)
	{
		String[] splitInfo = info.Split(";");
		int infoIndex = 0;

		int inventoryIndex = int.Parse(splitInfo[infoIndex]);
		infoIndex++;

		TeamMapAI teamMapAI = _gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[int.Parse(splitInfo[infoIndex])];
		infoIndex++;

		Agent agent = teamMapAI.GetUnit(splitInfo[infoIndex]);
		infoIndex++;


		if (agent != null && IsInstanceValid(agent))
		{
			Inventory inventory = agent.GetInventory();

			if (inventory != null && IsInstanceValid(inventory))
			{
				if (inventory.GetItem(inventoryIndex) != null && !inventory.IsItemIndexInUsed(inventoryIndex))
				{
					ItemResource itemResource = inventory.GetItem(inventoryIndex);

					inventory.RemoveItem(inventoryIndex);
					if (itemResource.ItemID == "SYC-010")
					{
						agent.Heal(agent.MaxHealth);
					}
				}

				// Sync the client no matter if is passing (if not passing, it may indicate cliet is out of sync with server inventory so need to sync)
				if (GetTree().NetworkPeer != null && GetTree().IsNetworkServer())
				{
					SyncInventory(-1, agent);
				}
			}
		}
	}


}
