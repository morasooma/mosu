// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Entities;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Textures;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Editor
{
    /// <summary>
    /// The operations the editor panel asks the screen to carry out.
    /// </summary>
    internal interface IDodgeWorldEditorHost
    {
        void ApplyRoomProperties(string name, string width, string height);
        void SetSpawnAtPlayer();

        void AddSurface();
        void AddCollisionZone();
        void AddNpc();
        void AddMobSpawnZone();
        void AddEmitter();
        void AddBeam();
        void AddWarp();
        void AddPortal();
        void AddSidePassage();
        void AddTerminal();

        /// <summary>Adds a kiosk already set up as a shop, rather than one to be switched over by hand.</summary>
        void AddShop();

        void CreateConnectedRoom();

        /// <summary>Asks which room the selected passage leads to, on the world map.</summary>
        void LinkSelectedPassage();

        /// <summary>Points the selected passage back at the room the author came from.</summary>
        void LinkSelectedPassageToPreviousRoom();

        /// <summary>
        /// Says that the middle of the view is where the player should come out, and asks which room they
        /// will be arriving from.
        /// </summary>
        void SetArrivalHere();

        void OpenLinkedRoom();
        void ReturnToRootRoom();
        void ToggleWorldMap();

        /// <summary>Makes the current room the one the world starts in, and the one death returns to.</summary>
        void MakeThisRoomInitial();

        /// <summary>Removes the current room and unpoints the passages that led to it. Asks twice.</summary>
        void DeleteThisRoom();

        void PublishWorld();
        void ResetRoom();
        void CloseEditor();

        /// <summary>Restores the room to before the last change.</summary>
        void Undo();

        void AdjustSpawnCount(int delta);
        void CycleSelectedStyle();
        void ToggleSelectedShape();
        void DeleteSelected();

        /// <summary>Copies the selection, so it can be put down again here or in another room.</summary>
        void CopySelected();

        void PasteCopied();

        /// <summary>Copies the selection next to itself, for laying out a row of the same thing.</summary>
        void DuplicateSelected();

        /// <summary>Draws the selection over the objects it shares a depth with.</summary>
        void BringSelectionToFront();

        void SendSelectionToBack();

        /// <summary>Called after the inspector writes new values onto an entity.</summary>
        void OnSelectionEdited();

        void ImportTexture();
        void ApplySelectedTextureToSurface();
        void ApplySelectedTextureToWeapon();
        void ClearSurfaceTexture();
        void CycleTextureFill();
        void ToggleTextureSmoothing();
        void SelectTextureAsset(string assetId);

        void SetSelectedCornerRadius(float value);
        void SetSelectedTextureOpacity(float value);

        /// <summary>Sets what one swing of the world's sword takes off a mob.</summary>
        void SetSwordDamage(int damage);
    }

    /// <summary>
    /// The world editor's side panel.
    /// </summary>
    internal partial class DodgeWorldEditorPanel : CompositeDrawable
    {
        /// <summary>
        /// Panel width. The screen insets the world by this while the editor is open, so that the
        /// right-hand edge of an object — and its resize handles — are not left underneath the panel.
        /// </summary>
        public const float PANEL_WIDTH = 420;

        private enum Section
        {
            Room,
            Object,
            Dialogue,
            Material,
            World,
        }

        public EntityInspector Inspector { get; }

        public DialogueEditor Dialogue { get; }

        /// <summary>
        /// The corner rounding of the selected surface. Named apart from the panel's own inherited
        /// <c>CornerRadius</c>, which has nothing to do with what is being edited.
        /// </summary>
        public BindableFloat SurfaceCornerRadius { get; } = new BindableFloat { MinValue = 0, MaxValue = 240 };

        public BindableFloat TextureOpacity { get; } = new BindableFloat(1) { MinValue = 0, MaxValue = 1 };

        /// <summary>
        /// What one swing takes off a mob. A property of the world's sword rather than of any selection,
        /// which is why it sits with the weapon instead of in the object inspector.
        /// </summary>
        public BindableInt SwordDamage { get; } = new BindableInt(WeaponSkin.DEFAULT_DAMAGE)
        {
            MinValue = WeaponSkin.MIN_DAMAGE,
            // Not the stored maximum: a slider that reaches a thousand cannot be set to seven. Numbers
            // past this belong to a world that is being balanced through the API.
            MaxValue = 50,
        };

        private readonly OsuColour colours;
        private readonly IDodgeWorldEditorHost host;
        private readonly DodgeWorldTextureLibrary library;

        private readonly Dictionary<Section, Drawable> sections = new Dictionary<Section, Drawable>();
        private readonly Dictionary<Section, RoundedButton> tabs = new Dictionary<Section, RoundedButton>();

        private readonly OsuTextFlowContainer status;
        private readonly OsuSpriteText title;
        private readonly OsuTextFlowContainer textureSelection;
        private readonly FillFlowContainer textureCards;
        private readonly RoundedButton fillButton;
        private readonly RoundedButton smoothingButton;

        private readonly LabelledEditorField roomName;
        private readonly LabelledEditorField roomWidth;
        private readonly LabelledEditorField roomHeight;

        public DodgeWorldEditorPanel(OsuColour colours, WorldEntityRegistry registry, DodgeWorldTextureLibrary library,
                                     IDodgeWorldEditorHost host)
        {
            this.colours = colours;
            this.host = host;
            this.library = library;

            Inspector = new EntityInspector(registry, colours, host.OnSelectionEdited);
            Dialogue = new DialogueEditor(colours, SetStatus);

            RelativeSizeAxes = Axes.Y;
            Width = PANEL_WIDTH;
            Anchor = Anchor.TopRight;
            Origin = Anchor.TopRight;
            Alpha = 0;

            sections[Section.Room] = section(
                heading("ПАРАМЕТРЫ КОМНАТЫ", colours.Pink1, 15),
                roomName = new LabelledEditorField("Название комнаты", "показывается в HUD", string.Empty, colours, applyRoom),
                roomWidth = new LabelledEditorField("Ширина", "800–6000 единиц", string.Empty, colours, applyRoom),
                roomHeight = new LabelledEditorField("Высота", "600–4000 единиц", string.Empty, colours, applyRoom),
                button("Применить параметры комнаты", applyRoom),
                button("Поставить точку входа под игроком", host.SetSpawnAtPlayer));

            sections[Section.Object] = section(
                Inspector,
                button("Применить свойства объекта", Inspector.Apply),
                button("Больше мобов в зоне", () => host.AdjustSpawnCount(1)),
                button("Меньше мобов в зоне", () => host.AdjustSpawnCount(-1)),
                button("Сменить вид / цвет", host.CycleSelectedStyle),
                button("Переключить скругление", host.ToggleSelectedShape),
                heading("КОПИИ И СЛОИ", colours.Pink1, 15),
                heading("Копия сохраняет все свойства, включая диалог и текстуру, и переносится между комнатами."
                        + " Слой решает, кто сверху, когда объекты стоят на одной глубине.", colours.GrayA),
                button("Дублировать (Ctrl+D)", host.DuplicateSelected),
                button("Копировать (Ctrl+C)", host.CopySelected),
                button("Вставить (Ctrl+V)", host.PasteCopied),
                button("На передний план (Ctrl+])", host.BringSelectionToFront),
                button("На задний план (Ctrl+[)", host.SendSelectionToBack),
                button("Удалить выбранный объект", host.DeleteSelected),
                heading("СЮЖЕТ — КАК СОБРАТЬ СЦЕНУ", colours.Pink1, 15),
                heading("Флаг — это счётчик у игрока. Он поднимается до указанного числа, а не прибавляется,"
                        + " поэтому поговорить дважды — не пройти дважды.", colours.GrayA),
                heading("1. NPC: «Ставит флаг» → mora.met, «Значение флага» → 1. Флаг ставится, когда игрок"
                        + " дослушал ветку до конца, а не когда открыл диалог. Сама сцена — во вкладке «Диалог»:"
                        + " одна ветка читается по порядку, а вторая ветка — это то, что персонаж скажет в"
                        + " следующий разговор.", colours.GrayA),
                heading("2. Награда: опыт и монеты выдаются один раз — в тот же момент, когда флаг поднялся"
                        + " впервые. Начисляет сервер, по числам из опубликованного мира.", colours.GrayA),
                heading("3. Что должно появиться: у двери, прохода, варпа, зоны мобов — «Виден, если флаг» →"
                        + " mora.met, «не меньше» → 1. До разговора объекта нет вообще, после — есть, и комната"
                        + " перестраивается сразу, без выхода из неё.", colours.GrayA),
                heading("4. Следующий шаг — тот же флаг с большим значением: 2, 3, 4. «и меньше» убирает объект"
                        + " снова, когда история ушла дальше — так делается то, что бывает только в одной главе.",
                    colours.GrayA),
                heading("Неизвестный флаг равен нулю, поэтому сюжет можно переписывать: старый прогресс не"
                        + " ломается, а условие, которого раньше не было, просто ещё не выполнено.", colours.GrayA));

            sections[Section.Dialogue] = section(Dialogue);

            sections[Section.Material] = section(
                heading("МАТЕРИАЛ ОБЪЕКТА", colours.Pink1, 15),
                heading("Текстуру берут поверхности, NPC и зоны мобов. У зоны это внешний вид её мобов."
                        + " Stretch тянет, Fill обрезает, Fit вписывает, Tile повторяет — один пиксель"
                        + " картинки на одну единицу мира, поэтому плитка 128×128 покрывает комнату любого размера.",
                    colours.GrayA),
                heading("РАДИУС УГЛОВ", colours.GrayA),
                new RoundedSliderBar<float>
                {
                    RelativeSizeAxes = Axes.X,
                    Current = SurfaceCornerRadius,
                    AccentColour = colours.Pink1,
                },
                heading("ПРОЗРАЧНОСТЬ ТЕКСТУРЫ", colours.GrayA),
                new RoundedSliderBar<float>
                {
                    RelativeSizeAxes = Axes.X,
                    Current = TextureOpacity,
                    DisplayAsPercentage = true,
                    AccentColour = colours.Pink1,
                },
                fillButton = button("Заполнение текстуры: Fill", host.CycleTextureFill),
                smoothingButton = button("Сглаживание: Linear", host.ToggleTextureSmoothing),
                heading("БИБЛИОТЕКА ТЕКСТУР", colours.Pink1, 15),
                textureSelection = heading("Ничего не выбрано", colours.GrayA),
                new OsuScrollContainer(Direction.Horizontal)
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 112,
                    Child = textureCards = new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(8),
                    },
                },
                button("Добавить PNG/JPG в библиотеку", host.ImportTexture),
                button("Применить к выбранному объекту", host.ApplySelectedTextureToSurface),
                button("Использовать выбранную как меч", host.ApplySelectedTextureToWeapon),
                heading("УРОН МЕЧА", colours.GrayA),
                heading("Сколько снимает один удар. Здоровье моба задаётся в его зоне, так что это две"
                        + " половины одного числа: сколько ударов он держит.", colours.GrayA),
                new RoundedSliderBar<int>
                {
                    RelativeSizeAxes = Axes.X,
                    Current = SwordDamage,
                    AccentColour = colours.Pink1,
                },
                button("Убрать текстуру с объекта", host.ClearSurfaceTexture));

            sections[Section.World] = section(
                heading("ОБЪЕКТЫ", colours.Pink1, 15),
                button("Добавить панель комнаты", host.AddSurface),
                button("Добавить зону коллизии", host.AddCollisionZone),
                button("Добавить NPC", host.AddNpc),
                heading("NPC — это и персонаж, и предмет: поставь «Живое существо: нет», и над ним не будет"
                        + " имени, а подсказка станет коротким «E». Ширина, высота и заполнение текстуры —"
                        + " во вкладке «Объект» и «Материал», а что он говорит — во вкладке «Диалог»:"
                        + " ветка читается целиком, следующий разговор берёт следующую ветку.", colours.GrayA),
                button("Добавить доску заданий", host.AddTerminal),
                button("Добавить магазин", host.AddShop),
                button("Добавить зону появления мобов", host.AddMobSpawnZone),
                heading("ПОЛОСА ПРЕПЯТСТВИЙ", colours.Pink1, 15),
                heading("Работают по кругу и ни на кого не реагируют. Расставляются сдвигом фазы: одинаковый цикл, разный сдвиг — получается волна.", colours.GrayA),
                button("Добавить эмиттер", host.AddEmitter),
                button("Добавить луч", host.AddBeam),
                heading("СВЯЗИ МЕЖДУ КОМНАТАМИ", colours.Pink1, 15),
                heading("Портал и проход ведут в комнату; варп — платная сеть телепортов. Проходы работают"
                        + " парами: когда связываешь проход с комнатой, он сам находит там обратный проход и"
                        + " берёт его в пару — игрок выйдет именно у него. Поэтому два коридора между одними и"
                        + " теми же комнатами больше не путаются.", colours.GrayA),
                heading("Выбери проход — снизу написано, куда он ведёт и у какого прохода выйдет игрок. В"
                        + " комнате это видно крестом: жёлтый — точка задана вручную, синий — посчитана"
                        + " автоматически. Внутрь стены попасть нельзя: игрока вынесет на ближайшее свободное"
                        + " место.", colours.GrayA),
                button("Добавить портал", host.AddPortal),
                button("Добавить боковой проход", host.AddSidePassage),
                button("Добавить варп", host.AddWarp),
                button("Куда ведёт проход — выбрать на карте", host.LinkSelectedPassage),
                button("Связать проход с предыдущей комнатой", host.LinkSelectedPassageToPreviousRoom),
                button("Сюда приходить из… (точка выхода)", host.SetArrivalHere),
                button("Создать комнату из выбранного прохода", host.CreateConnectedRoom),
                button("Открыть связанную комнату", host.OpenLinkedRoom),
                button("Вернуться в начальную комнату", host.ReturnToRootRoom),
                button("Карта мира (M)", host.ToggleWorldMap),
                heading("МИР", colours.Pink1, 15),
                heading("Начальная комната — та, в которой мир начинается и в которую возвращает смерть."
                        + " Её же карта мира считает началом отсчёта. Удалить начальную комнату нельзя:"
                        + " сначала сделай начальной другую.", colours.GrayA),
                button("Сделать эту комнату начальной", host.MakeThisRoomInitial),
                button("Удалить эту комнату", host.DeleteThisRoom),
                button("Отменить последнее действие (Ctrl+Z)", host.Undo),
                button("Сохранить / опубликовать мир", host.PublishWorld),
                button("Сбросить текущую комнату", host.ResetRoom),
                button("Закрыть редактор (F2 или Esc)", host.CloseEditor));

            var tabFlow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(5),
            };

            addTab(tabFlow, Section.Room, "Комната");
            addTab(tabFlow, Section.Object, "Объект");
            addTab(tabFlow, Section.Dialogue, "Диалог");
            addTab(tabFlow, Section.Material, "Материал");
            addTab(tabFlow, Section.World, "Мир");

            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray1.Opacity(0.98f) },
                new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    // Clears the game's toolbar, which overlays the top of the screen.
                    Padding = new MarginPadding { Top = 62, Bottom = 20, Horizontal = 18 },
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 10),
                        Children = new Drawable[]
                        {
                            title = new OsuSpriteText
                            {
                                Text = "РЕДАКТОР DODGE WORLD",
                                Font = OsuFont.Default.With(size: 24, weight: FontWeight.Bold),
                                Colour = colours.Pink1,
                            },
                            WrappedText.Paragraph(
                                "F2 или Esc — закрыть • Ctrl+Z — отменить • Ctrl+S — опубликовать • перетаскивание — переместить"
                                + " • стрелки — сдвиг на 16 (Shift — на 1) • Ctrl+D — дублировать • Ctrl+C/Ctrl+V — копировать"
                                + " • Ctrl+] / Ctrl+[ — слой • Delete — удалить • колесо — масштаб • Space+ЛКМ — камера"
                                + " • M — карта мира • картинку можно просто бросить в окно • Alt+стрелки и Alt+колесо —"
                                + " громкость игры • чат тянется за заголовок и за уголок", colours.GrayA),
                            tabFlow,
                            new Box { RelativeSizeAxes = Axes.X, Height = 2, Colour = colours.Gray3 },
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Children = sections.Values.ToArray(),
                            },
                            status = WrappedText.Paragraph("Выбери объект на карте или раздел редактора.", colours.Gray9, 13),
                        },
                    },
                },
            };

            SurfaceCornerRadius.BindValueChanged(value => host.SetSelectedCornerRadius(value.NewValue));
            TextureOpacity.BindValueChanged(value => host.SetSelectedTextureOpacity(value.NewValue));
            SwordDamage.BindValueChanged(value =>
            {
                // Showing the world's own number must not read as an edit of it: a world that carries no
                // sword of its own would be given one just by being opened.
                if (!showingSwordDamage)
                    host.SetSwordDamage(value.NewValue);
            });

            library.Changed += RebuildTextureBrowser;
            RebuildTextureBrowser();

            showSection(Section.Room);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            library.Changed -= RebuildTextureBrowser;
        }

        public void SetStatus(string text) => status.Text = text;

        private bool showingSwordDamage;

        /// <summary>
        /// Shows the world's sword damage without reporting it as an edit.
        /// </summary>
        public void ShowSwordDamage(int damage)
        {
            showingSwordDamage = true;
            SwordDamage.Value = damage;
            showingSwordDamage = false;
        }

        public void SetRoom(string name, Vector2 size)
        {
            roomName.SetValue(name);
            roomWidth.SetValue(EditorValue.Format(size.X));
            roomHeight.SetValue(EditorValue.Format(size.Y));
        }

        public void SetZoomDisplay(float zoom) => title.Text = $"РЕДАКТОР DODGE WORLD  •  {zoom:P0}";

        /// <summary>
        /// Points the inspector and the dialogue editor at a new selection.
        /// </summary>
        public void SetSelection(EditableWorldEntity? entity)
        {
            Inspector.SetTarget(entity);
            Dialogue.SetTarget(entity as InteractiveNpc);

            if (entity is RoomSurface surface)
                SurfaceCornerRadius.Value = surface.CornerRadiusValue;

            if (entity is ITexturedEntity textured)
            {
                TextureOpacity.Value = textured.TextureOpacity;
                SetMaterialLabels(textured);
            }
        }

        public void SetMaterialLabels(ITexturedEntity? entity)
        {
            // A character has no fill choice, so the button reports why rather than showing a value.
            fillButton.Text = $"Заполнение текстуры: {entity?.TextureFillModeName ?? "—"}";
            smoothingButton.Text = $"Сглаживание: {(entity == null ? "—" : entity.TextureSmoothing ? "Linear" : "Nearest")}";
        }

        public void RebuildTextureBrowser()
        {
            textureCards.Clear();

            foreach (TextureLibraryEntry entry in library.Entries.Values
                                                         .OrderByDescending(entry => entry.BuiltIn)
                                                         .ThenBy(entry => entry.Name))
            {
                var card = new TextureLibraryCard(entry.Id, entry.Name, colours, () => host.SelectTextureAsset(entry.Id))
                {
                    Selected = entry.Id == library.SelectedAssetId,
                };

                textureCards.Add(card);
                library.LoadPreview(card, entry);
            }

            textureSelection.Text = library.Selected == null ? "Ничего не выбрано" : $"Выбрано: {library.Selected.Name}";
        }

        private void applyRoom() => host.ApplyRoomProperties(roomName.Value, roomWidth.Value, roomHeight.Value);

        private void addTab(FillFlowContainer flow, Section target, string text)
        {
            var tab = new RoundedButton
            {
                Width = 78,
                Height = 28,
                Text = text,
                Action = () => showSection(target),
            };

            tabs[target] = tab;
            flow.Add(tab);
        }

        private void showSection(Section target)
        {
            foreach ((Section key, Drawable drawable) in sections)
                drawable.Alpha = key == target ? 1 : 0;

            foreach ((Section key, RoundedButton tab) in tabs)
                tab.Alpha = key == target ? 1 : 0.6f;
        }

        private FillFlowContainer section(params Drawable[] children) => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 8),
            Children = children,
        };

        private static OsuTextFlowContainer heading(string text, ColourInfo colour, float size = 12) =>
            WrappedText.Paragraph(text, colour, size, FontWeight.Bold);

        private RoundedButton button(string text, Action action) => new RoundedButton
        {
            RelativeSizeAxes = Axes.X,
            Height = 32,
            Text = text,
            Action = action,
        };
    }
}
