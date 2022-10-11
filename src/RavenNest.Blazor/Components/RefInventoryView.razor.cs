using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using RavenNest.Blazor.Services;
using RavenNest.BusinessLogic.Extended;
using RavenNest.BusinessLogic.Providers;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Components
{
    public partial class RefInventoryView : ComponentBase
    {
        [Inject]
        PlayerService CharacterService { get; set; }
        [Inject]
        IPlayerInventoryProvider PlayerInventoryProvider { get; set; }
        [Inject]
        IWebHostEnvironment WebHostEnv { get; set; }
        [Inject]
        AuthService AuthService { get; set; }
        [Inject]
        ItemService ItemService { get; set; }
        [Parameter]
        public bool CanManage { get; set; }
        [Parameter]
        public WebsiteAdminUser SelectedUser { get; set; }
        public List<WebsiteAdminPlayer> Characters { get; set; }
        private Sessions.SessionInfo Session { get; set; }
        private bool CanModify { get => Session != null && Session.Administrator; }

        private bool ItemDetailDialogVisible;
        private InventoryItem ItemDetailsDialogItem;
        private long GiftOrSendAmount;

        private Item SelectedItem { get; set; }
        private bool[] AddItemDialogVisible = new bool[3];

        private IReadOnlyList<UserBankItem> _stash;

        private IReadOnlyList<UserBankItem> Stash
        {
            get { return _stash ?? new List<UserBankItem>(); }
            set { _stash = value; }
        }
        private Dictionary<Guid, Item> itemLookup;


        protected override void OnInitialized()
        {
            Session = AuthService.GetSession();
        }
        protected override async Task OnParametersSetAsync()
        {
            await base.OnParametersSetAsync();
            Characters = SelectedUser.Characters;
            var stash = SelectedUser.Stash;
            if (stash != null)
            {
                Stash = stash;
                this.itemLookup = stash.Select(x => x.ItemId)
                    .Distinct()
                    .Select(ItemService.GetItem)
                    .ToDictionary(x => x.Id, x => x);
            }

            return;
        }
        public void HideAddItem(int characterIndex)
        {
            AddItemDialogVisible[characterIndex] = false;
        }

        public void ShowAddItem(int characterIndex)
        {
            AddItemDialogVisible[characterIndex] = true;
        }

        public void AddItem(WebsiteAdminPlayer character)
        {
            if (SelectedItem == null)
                return;

            HideAddItem(character.CharacterIndex);

            CharacterService.AddItem(character.Id, SelectedItem);
            StateHasChanged();
            SelectedItem = null;
        }
        public string GetItemImage(Guid itemId, string tag)
        {
            if (tag != null)
            {
                return $"/api/twitch/logo/{tag}";
            }
            return $"/imgs/items/{itemId}.png";
        }

        public string GetSlotImageOrDefault(BusinessLogic.Providers.EquipmentSlot slot)
        {
            string path = "/imgs/icons/inventory_slot/";
            string none = "none.png"; //Default
            string outputSrc = path + slot.ToString().ToLower() + ".png";
            var wwwroot = WebHostEnv.WebRootPath;
            var testedSrc = File.Exists(wwwroot + outputSrc) ? outputSrc : path + none;

            return testedSrc;
        }

        public async Task<IEnumerable<Item>> SearchItem(string searchText)
        {
            return await ItemService.SearchAsync(searchText);
        }
        public Dictionary<BusinessLogic.Providers.EquipmentSlot, InventoryItem> GetEquipmentSlotAndItems(WebsiteAdminPlayer character)
        {
            Dictionary<BusinessLogic.Providers.EquipmentSlot, InventoryItem> equipmentSlotItems = new();
            foreach (BusinessLogic.Providers.EquipmentSlot slot in Enum.GetValues(typeof(BusinessLogic.Providers.EquipmentSlot)))
            {
                var ReadOnlyItem = PlayerInventoryProvider.Get(character.Id).GetEquippedItem(slot);
                var item = character.InventoryItems.FirstOrDefault(x => x.ItemId == ReadOnlyItem.ItemId);
                equipmentSlotItems.Add(slot, item);
            }

            return equipmentSlotItems;
        }

        public IReadOnlyList<InventoryItem> GetInventoryItems(WebsiteAdminPlayer character)
        {
            return character.InventoryItems.Where(x => !x.Equipped).ToList();
        }
        public string GetItemAmount(long item)
        {
            var value = item;
            if (value >= 1000_000)
            {
                var mils = value / 1000000.0;
                return Math.Round(mils) + "M";
            }
            else if (value > 1000)
            {
                var ks = value / 1000m;
                return Math.Round(ks) + "K";
            }

            return item.ToString();
        }
    }
}
