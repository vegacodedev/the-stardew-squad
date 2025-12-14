using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Menus;
using System;
using TheStardewSquad.Framework.Squad;

namespace TheStardewSquad.Framework.UI
{
    internal class SquadMemberMenu : IClickableMenu
    {
        private readonly IModHelper _helper;
        private readonly ISquadMate _mate;
        private readonly bool _isRecruited;
        private readonly Action<string> _onActionSelected;

        // UI Components
        private ClickableTextureComponent _portrait;
        private ClickableComponent _recruitButton;
        private ClickableComponent _inventoryButton;
        private ClickableComponent _waitHereButton;
        private ClickableComponent _dismissButton;
        private ClickableComponent _dismissAllButton;
        private ClickableComponent _closeButton;

        private string _npcName;
        private Texture2D _portraitTexture;
        private Rectangle _portraitSource;

        private const int MENU_WIDTH = 720;
        private const int MENU_HEIGHT = 500;
        private const int PORTRAIT_SIZE = 128;
        private const int BUTTON_WIDTH = 280;
        private const int BUTTON_HEIGHT = 80;
        private const int BUTTON_SPACING = 20;

        // Snap ID region constants for gamepad navigation
        public const int region_closeButton = 100;
        public const int region_recruitButton = 101;
        public const int region_inventoryButton = 102;
        public const int region_waitHereButton = 103;
        public const int region_dismissButton = 104;
        public const int region_dismissAllButton = 105;

        public SquadMemberMenu(IModHelper helper, ISquadMate mate, bool isRecruited, Action<string> onActionSelected)
            : base(
                x: Game1.uiViewport.Width / 2 - MENU_WIDTH / 2,
                y: Game1.uiViewport.Height / 2 - MENU_HEIGHT / 2,
                width: MENU_WIDTH,
                height: MENU_HEIGHT
            )
        {
            _helper = helper;
            _mate = mate;
            _isRecruited = isRecruited;
            _onActionSelected = onActionSelected;
            _npcName = mate.Npc is Pet pet ? pet.displayName : mate.Npc.displayName;

            InitializeComponents();
            LoadPortrait();

            // Initialize gamepad navigation if enabled
            if (Game1.options.SnappyMenus)
            {
                this.populateClickableComponentList();
                this.snapToDefaultClickableComponent();
            }
        }

        private void InitializeComponents()
        {
            // Portrait area (center, with space for name above)
            _portrait = new ClickableTextureComponent(
                bounds: new Rectangle(
                    xPositionOnScreen + width / 2 - PORTRAIT_SIZE / 2,
                    yPositionOnScreen + 135,
                    PORTRAIT_SIZE,
                    PORTRAIT_SIZE
                ),
                texture: null, // Will be set in LoadPortrait()
                sourceRect: Rectangle.Empty,
                scale: 1f
            );

            // Buttons (below portrait)
            int buttonY = yPositionOnScreen + 285;

            if (!_isRecruited)
            {
                // Recruit button (center)
                _recruitButton = new ClickableComponent(
                    bounds: new Rectangle(
                        xPositionOnScreen + width / 2 - BUTTON_WIDTH / 2,
                        buttonY,
                        BUTTON_WIDTH,
                        BUTTON_HEIGHT
                    ),
                    name: "recruit"
                )
                {
                    myID = region_recruitButton,
                    upNeighborID = region_closeButton,
                    downNeighborID = -1,
                    leftNeighborID = -1,
                    rightNeighborID = -1
                };
            }
            else
            {
                // Squad Inventory button (left top)
                _inventoryButton = new ClickableComponent(
                    bounds: new Rectangle(
                        xPositionOnScreen + width / 2 - BUTTON_WIDTH - BUTTON_SPACING / 2,
                        buttonY,
                        BUTTON_WIDTH,
                        BUTTON_HEIGHT
                    ),
                    name: "inventory"
                )
                {
                    myID = region_inventoryButton,
                    upNeighborID = region_closeButton,
                    downNeighborID = region_dismissButton,
                    leftNeighborID = -1,
                    rightNeighborID = region_waitHereButton
                };

                // Wait Here button (right top)
                _waitHereButton = new ClickableComponent(
                    bounds: new Rectangle(
                        xPositionOnScreen + width / 2 + BUTTON_SPACING / 2,
                        buttonY,
                        BUTTON_WIDTH,
                        BUTTON_HEIGHT
                    ),
                    name: "wait"
                )
                {
                    myID = region_waitHereButton,
                    upNeighborID = region_closeButton,
                    downNeighborID = region_dismissAllButton,
                    leftNeighborID = region_inventoryButton,
                    rightNeighborID = -1
                };

                // Dismiss button (left bottom)
                _dismissButton = new ClickableComponent(
                    bounds: new Rectangle(
                        xPositionOnScreen + width / 2 - BUTTON_WIDTH - BUTTON_SPACING / 2,
                        buttonY + BUTTON_HEIGHT + 10,
                        BUTTON_WIDTH,
                        BUTTON_HEIGHT
                    ),
                    name: "dismiss"
                )
                {
                    myID = region_dismissButton,
                    upNeighborID = region_inventoryButton,
                    downNeighborID = -1,
                    leftNeighborID = -1,
                    rightNeighborID = region_dismissAllButton
                };

                // Dismiss all button (right bottom)
                _dismissAllButton = new ClickableComponent(
                    bounds: new Rectangle(
                        xPositionOnScreen + width / 2 + BUTTON_SPACING / 2,
                        buttonY + BUTTON_HEIGHT + 10,
                        BUTTON_WIDTH,
                        BUTTON_HEIGHT
                    ),
                    name: "dismissAll"
                )
                {
                    myID = region_dismissAllButton,
                    upNeighborID = region_waitHereButton,
                    downNeighborID = -1,
                    leftNeighborID = region_dismissButton,
                    rightNeighborID = -1
                };
            }

            // Close button (top right)
            _closeButton = new ClickableComponent(
                bounds: new Rectangle(
                    xPositionOnScreen + width - 48,
                    yPositionOnScreen + 16,
                    48,
                    48
                ),
                name: "close"
            )
            {
                myID = region_closeButton,
                upNeighborID = -1,
                downNeighborID = !_isRecruited ? region_recruitButton : region_inventoryButton,
                leftNeighborID = -1,
                rightNeighborID = -1
            };
        }

        private void LoadPortrait()
        {
            if (_mate.Npc is Pet pet)
            {
                // For pets, use their sprite texture
                _portraitTexture = pet.Sprite.Texture;
                // Get front-facing frame (usually frame 0)
                _portraitSource = pet.Sprite.SourceRect;
            }
            else
            {
                // For NPCs, use their portrait property which respects Content Patcher changes
                try
                {
                    // Use NPC.Portrait which automatically updates with Content Patcher
                    _portraitTexture = _mate.Npc.Portrait;
                    _portraitSource = new Rectangle(0, 0, 64, 64);

                    // If Portrait property is null, fall back to loading directly
                    if (_portraitTexture == null)
                    {
                        _portraitTexture = Game1.content.Load<Texture2D>($"Portraits\\{_mate.Npc.Name}");
                    }
                }
                catch
                {
                    // Fallback to sprite if portrait doesn't exist
                    _portraitTexture = _mate.Npc.Sprite.Texture;
                    _portraitSource = _mate.Npc.Sprite.SourceRect;
                }
            }

            if (_portrait != null && _portraitTexture != null)
            {
                _portrait.texture = _portraitTexture;
                _portrait.sourceRect = _portraitSource;
            }
        }

        public override void snapToDefaultClickableComponent()
        {
            // Default to the primary action button
            int defaultId = _isRecruited ? region_inventoryButton : region_recruitButton;

            this.currentlySnappedComponent = this.getComponentWithID(defaultId);
            this.snapCursorToCurrentSnappedComponent();
        }

        public override void receiveGamePadButton(Buttons b)
        {
            base.receiveGamePadButton(b);

            // B button closes the menu (standard back/cancel button)
            if (b == Buttons.B)
            {
                this.exitThisMenu();
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (_closeButton?.containsPoint(x, y) ?? false)
            {
                exitThisMenu(playSound);
                return;
            }

            if (_recruitButton?.containsPoint(x, y) ?? false)
            {
                if (playSound) Game1.playSound("drumkit6");
                _onActionSelected("recruit");
                exitThisMenu(false);
                return;
            }

            if (_inventoryButton?.containsPoint(x, y) ?? false)
            {
                if (playSound) Game1.playSound("drumkit6");
                _onActionSelected("inventory");
                exitThisMenu(false);
                return;
            }

            if (_waitHereButton?.containsPoint(x, y) ?? false)
            {
                if (playSound) Game1.playSound("drumkit6");
                _onActionSelected("wait");
                exitThisMenu(false);
                return;
            }

            if (_dismissButton?.containsPoint(x, y) ?? false)
            {
                if (playSound) Game1.playSound("drumkit6");
                _onActionSelected("dismiss");
                exitThisMenu(false);
                return;
            }

            if (_dismissAllButton?.containsPoint(x, y) ?? false)
            {
                if (playSound) Game1.playSound("drumkit6");
                _onActionSelected("dismissAll");
                exitThisMenu(false);
                return;
            }

            // Click outside menu bounds - close the menu
            if (!this.isWithinBounds(x, y))
            {
                this.exitThisMenu(playSound);
                return;
            }
        }

        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
            base.receiveRightClick(x, y, playSound);

            // Right-click anywhere closes the menu
            this.exitThisMenu(playSound);
        }

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            // Hover states are handled in the draw method via containsPoint checks
        }

        public override void draw(SpriteBatch b)
        {
            // Draw fade background
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);

            // Draw menu background
            Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, width, height, false, true);

            // Draw NPC name (inside UI, above portrait)
            string nameText = _npcName;
            Vector2 nameSize = Game1.dialogueFont.MeasureString(nameText);
            Vector2 namePosition = new Vector2(
                xPositionOnScreen + width / 2 - nameSize.X / 2,
                yPositionOnScreen + 100
            );
            Utility.drawTextWithShadow(b, nameText, Game1.dialogueFont, namePosition, Game1.textColor);

            // Draw portrait/sprite
            if (_portraitTexture != null)
            {
                b.Draw(
                    texture: _portraitTexture,
                    destinationRectangle: _portrait.bounds,
                    sourceRectangle: _portraitSource,
                    color: Color.White
                );
            }

            // Draw recruitment status
            string statusText = _isRecruited
                ? _helper.Translation.Get("ui.status.recruited")
                : _helper.Translation.Get("ui.status.notRecruited");
            Vector2 statusSize = Game1.smallFont.MeasureString(statusText);
            Vector2 statusPosition = new Vector2(
                xPositionOnScreen + width / 2 - statusSize.X / 2,
                yPositionOnScreen + 255
            );
            Utility.drawTextWithShadow(b, statusText, Game1.smallFont, statusPosition,
                _isRecruited ? Color.LightGreen : Color.Gray);

            // Draw buttons
            if (!_isRecruited)
            {
                DrawButton(b, _recruitButton, _helper.Translation.Get("ui.button.recruit"));
            }
            else
            {
                DrawButton(b, _inventoryButton, _helper.Translation.Get("squad.inventory.open"));

                // For pets, show "Roam here" instead of "Wait here"
                string waitButtonText = _mate.Npc is Pet
                    ? _helper.Translation.Get("ui.button.roamHere")
                    : _helper.Translation.Get("ui.button.waitHere");
                DrawButton(b, _waitHereButton, waitButtonText);

                DrawButton(b, _dismissButton, _helper.Translation.Get("ui.button.dismiss"));
                DrawButton(b, _dismissAllButton, _helper.Translation.Get("ui.button.dismissAll"));
            }

            // Draw close button (X)
            DrawCloseButton(b);

            // Draw cursor
            drawMouse(b);
        }

        private void DrawButton(SpriteBatch b, ClickableComponent button, string text)
        {
            if (button == null) return;

            // Highlight if mouse hover OR gamepad selected
            bool isHighlighted = button.containsPoint(Game1.getMouseX(), Game1.getMouseY()) ||
                                (this.currentlySnappedComponent == button);

            Color buttonColor = isHighlighted ? Color.Wheat : Color.White;

            IClickableMenu.drawTextureBox(
                b,
                Game1.mouseCursors,
                new Rectangle(432, 439, 9, 9),
                button.bounds.X,
                button.bounds.Y,
                button.bounds.Width,
                button.bounds.Height,
                buttonColor,
                4f,
                false
            );

            // Button text
            Vector2 textSize = Game1.dialogueFont.MeasureString(text);
            Vector2 textPosition = new Vector2(
                button.bounds.X + button.bounds.Width / 2 - textSize.X / 2,
                button.bounds.Y + button.bounds.Height / 2 - textSize.Y / 2
            );

            Utility.drawTextWithShadow(b, text, Game1.dialogueFont, textPosition, Game1.textColor);
        }

        private void DrawCloseButton(SpriteBatch b)
        {
            // Draw X button
            b.Draw(
                Game1.mouseCursors,
                new Vector2(_closeButton.bounds.X, _closeButton.bounds.Y),
                new Rectangle(337, 494, 12, 12),
                Color.White,
                0f,
                Vector2.Zero,
                4f,
                SpriteEffects.None,
                1f
            );
        }
    }
}
