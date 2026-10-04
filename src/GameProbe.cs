using Il2CppInterop.Runtime;
using System;

namespace AstralFocus
{
    /// <summary>
    /// 只读地读取游戏状态。所有访问都走 il2cpp 原始 API，
    /// 因此能覆盖 HybridCLR 在运行时加载的热更类型。
    /// </summary>
    internal sealed class GameProbe
    {
        private IntPtr _clsManager, _clsBattle, _clsFight, _clsAccount, _clsUiManager, _clsRoundCard;

        private IntPtr _fInst, _fAccount, _fBattle, _fFight, _fCurPlayerId,
                       _fFightType, _fFightData, _fRoundCardWindow, _fRoundCardSn,
                       _fUiInst, _fDefenderField, _fRolePlayerId;
        private IntPtr _mGetInst, _mUiGetInst;   // 静态属性 getter，泛型静态字段最可靠的读法
        private IntPtr _fVisible;               // FairyGUI.GObject._visible
        private IntPtr _fFightWindow;           // UIManager._cachedFightWindow
        private IntPtr _fDodgeChoice;           // FightWindow.dodgeChoice
        private IntPtr _fRoundCardIds;          // ChooseRoundCardWindow._roundCardIds
        private IntPtr _clsFightWindow;
        // 防守方身份链：FightWindow._DefenderData -> BattlePlayerData.player -> RoomPlayer.serverPlayer -> Player.id_
        private IntPtr _fDefenderData;
        private IntPtr _fBpdPlayer;
        private IntPtr _fRoomPlayerServerPlayer;
        private IntPtr _fPlayerModelId;
        private IntPtr _fAccountPlayer;   // AccountLogic.player (party.model.Player)
        private IntPtr _fUnclearedWindows;   // UIManager._unclearedWindows (List<BaseWindow>)
        private IntPtr _fPropUpWindows;      // UIManager.propUpWindows (Stack<BaseWindow>)
        private IntPtr _fParentBacking;      // FairyGUI.GObject.<parent>k__BackingField（非空 = 正在显示）
        private IntPtr _fContentPane;       // FairyGUI.Window._contentPane (GComponent)
        private IntPtr _fBtnDefend;          // UI.UIFightWindow.btn_Defend (GButton)

        public bool Ready { get; private set; }
        public string LastError { get; private set; }

        private static void Step(string m) => Diagnostics.Log("probe: " + m);

        public void Initialize()
        {
            Ready = false;
            LastError = null;
            try
            {
                Step("domain");
                Il2Cpp.Domain();
                Step("enum-assemblies");
                Step("find GameLogicManager");
                _clsManager   = Il2Cpp.FindClass("GameLogic", "GameLogicManager");
                Step("found mgr=" + (_clsManager != IntPtr.Zero));
                Step("find BattleLogic");
                _clsBattle    = Il2Cpp.FindClass("GameLogic", "BattleLogic");
                Step("find FightLogic");
                _clsFight     = Il2Cpp.FindClass("GameLogic", "FightLogic");
                Step("find AccountLogic");
                _clsAccount   = Il2Cpp.FindClass("GameLogic", "AccountLogic");
                Step("find UIManager");
                _clsUiManager = Il2Cpp.FindClass("UI", "UIManager");
                Step("find ChooseRoundCardWindow");
                _clsRoundCard = Il2Cpp.FindClass("UI", "ChooseRoundCardWindow");
                Step("all classes: mgr=" + (_clsManager != IntPtr.Zero) + " battle=" + (_clsBattle != IntPtr.Zero) + " fight=" + (_clsFight != IntPtr.Zero) + " ui=" + (_clsUiManager != IntPtr.Zero));

                if (_clsManager == IntPtr.Zero || _clsBattle == IntPtr.Zero || _clsFight == IntPtr.Zero)
                {
                    LastError = "未找到热更类型（游戏可能尚未进入可玩状态，或版本已更新）";
                    return;
                }

                Step("field _inst");
                _fInst = FieldOnGenericBase(_clsManager, "_inst");
                Step("_inst field=" + (_fInst != IntPtr.Zero));
                _mGetInst = Il2Cpp.Method(_clsManager, "get_inst", 0);
                if (_fInst == IntPtr.Zero && _mGetInst == IntPtr.Zero) { LastError = "未找到单例入口 _inst/get_inst"; return; }
                if (_clsUiManager != IntPtr.Zero)
                {
                    _fUiInst = FieldOnGenericBase(_clsUiManager, "_inst");
                    _mUiGetInst = Il2Cpp.Method(_clsUiManager, "get_inst", 0);
                }

                _fAccount = Il2Cpp.Field(_clsManager, "<account>k__BackingField");
                _fBattle  = Il2Cpp.Field(_clsManager, "<battle>k__BackingField");
                _fFight   = Il2Cpp.Field(_clsManager, "<fight>k__BackingField");

                _fCurPlayerId = Il2Cpp.Field(_clsBattle, "CurPlayerId");
                _fFightType   = Il2Cpp.Field(_clsFight, "fightType");
                _fFightData   = Il2Cpp.Field(_clsFight, "battleFightData");

                if (_clsRoundCard != IntPtr.Zero)
                    _fRoundCardSn = Il2Cpp.Field(_clsRoundCard, "_sn");

                if (_clsUiManager != IntPtr.Zero)
                    _fRoundCardWindow = Il2Cpp.Field(_clsUiManager, "_cachedChooseRoundCardWindow");
                    _fUnclearedWindows = Il2Cpp.Field(_clsUiManager, "_unclearedWindows");
                    _fPropUpWindows = Il2Cpp.Field(_clsUiManager, "propUpWindows");
                    Step("_unclearedWindows=" + _fUnclearedWindows + " propUpWindows=" + _fPropUpWindows);

                Step("find BattleFightData");
                var clsFightData = Il2Cpp.FindClass("GameLogic", "BattleFightData");
                if (clsFightData != IntPtr.Zero) _fDefenderField = Il2Cpp.Field(clsFightData, "defenderInfo");

                Step("find BattleRole");
                Step("find FightWindow");
                _clsFightWindow = Il2Cpp.FindClass("UI", "FightWindow");
                if (_clsFightWindow != IntPtr.Zero)
                {
                    _fDodgeChoice = Il2Cpp.Field(_clsFightWindow, "dodgeChoice");
                    _fContentPane = Il2Cpp.Field(_clsFightWindow, "_contentPane");
                    Step("dodgeChoice=" + _fDodgeChoice + " _contentPane=" + _fContentPane);
                }
                var clsUiFight = Il2Cpp.FindClass("UI", "UIFightWindow");
                if (clsUiFight != IntPtr.Zero)
                {
                    _fBtnDefend = Il2Cpp.Field(clsUiFight, "btn_Defend");
                    Step("btn_Defend=" + _fBtnDefend);
                }
                if (_clsUiManager != IntPtr.Zero)
                {
                    _fFightWindow = Il2Cpp.Field(_clsUiManager, "_cachedFightWindow");
                    Step("_cachedFightWindow=" + _fFightWindow);
                }
                if (_clsRoundCard != IntPtr.Zero)
                {
                    _fRoundCardIds = Il2Cpp.Field(_clsRoundCard, "_roundCardIds");
                }

                Step("解析防守方身份字段链");
                var clsBpd = Il2Cpp.FindClass("GameLogic", "BattlePlayerData");
                var clsRoomPlayer = Il2Cpp.FindClass("GameLogic", "RoomPlayer");
                var clsPlayerModel = Il2Cpp.FindClass("party.model", "Player");
                if (_clsAccount != IntPtr.Zero) _fAccountPlayer = Il2Cpp.Field(_clsAccount, "player");
                if (_clsFightWindow != IntPtr.Zero) _fDefenderData = Il2Cpp.Field(_clsFightWindow, "_DefenderData");
                if (clsBpd != IntPtr.Zero) _fBpdPlayer = Il2Cpp.Field(clsBpd, "player");
                if (clsRoomPlayer != IntPtr.Zero) _fRoomPlayerServerPlayer = Il2Cpp.Field(clsRoomPlayer, "serverPlayer");
                if (clsPlayerModel != IntPtr.Zero) _fPlayerModelId = Il2Cpp.Field(clsPlayerModel, "id_");
                Step("身份链字段: defenderData=" + _fDefenderData + " bpdPlayer=" + _fBpdPlayer +
                     " serverPlayer=" + _fRoomPlayerServerPlayer + " playerId=" + _fPlayerModelId);

                Step("find FairyGUI.GObject");
                var clsGObject = Il2Cpp.FindClass("FairyGUI", "GObject");
                if (clsGObject != IntPtr.Zero)
                {
                    _fVisible = Il2Cpp.Field(clsGObject, "_visible");
                    _fParentBacking = Il2Cpp.Field(clsGObject, "<parent>k__BackingField");
                    Step("_fVisible=" + _fVisible + " _fParentBacking=" + _fParentBacking);
                }

                var clsRole = Il2Cpp.FindClass("party.model", "BattleRole");
                if (clsRole != IntPtr.Zero) _fRolePlayerId = Il2Cpp.Field(clsRole, "playerId_");

                Step("字段指针: _fInst=" + _fInst + " _fUiInst=" + _fUiInst +
                     " _fAccount=" + _fAccount + " _fBattle=" + _fBattle + " _fFight=" + _fFight +
                     " _fCurPlayerId=" + _fCurPlayerId + " _fFightType=" + _fFightType +
                     " _fFightData=" + _fFightData + " _fRoundCardWindow=" + _fRoundCardWindow +
                     " _fRoundCardSn=" + _fRoundCardSn + " _fDefenderField=" + _fDefenderField +
                     " _fRolePlayerId=" + _fRolePlayerId);
                Step("UIManager 基类 _inst 查找: " + (_fUiInst != IntPtr.Zero));
                Ready = true;
                Step("READY");
            }
            catch (Exception e)
            {
                LastError = e.Message;
            }
        }

        private static IntPtr FieldOnGenericBase(IntPtr klass, string name)
        {
            var f = Il2Cpp.Field(klass, name);
            if (f != IntPtr.Zero) return f;
            var parent = IL2CPP.il2cpp_class_get_parent(klass);
            return parent == IntPtr.Zero ? IntPtr.Zero : Il2Cpp.Field(parent, name);
        }

        /// <summary>
        /// 只读单例静态字段。刻意不调用 get_inst —— 它可能惰性 new 出单例，
        /// 在后台线程分配对象有风险；游戏自己会在主线程初始化好。
        /// </summary>
        private static IntPtr ReadSingleton(IntPtr field)
        {
            if (field == IntPtr.Zero) return IntPtr.Zero;
            return Il2Cpp.StaticObject(field);
        }

        private IntPtr Manager() => ReadSingleton(_fInst);

        private IntPtr Sub(IntPtr field)
        {
            var m = Manager();
            return (m == IntPtr.Zero || field == IntPtr.Zero) ? IntPtr.Zero : Il2Cpp.FieldObject(m, field);
        }

        private long _cachedSelfId;

        /// <summary>
        /// 本地玩家 ID。直接读 AccountLogic.player.id_，避免每轮都做昂贵的
        /// il2cpp 反射调用（il2cpp_class_get_method_from_name + runtime_invoke 会线性扫描方法表）。
        /// </summary>
        public long SelfPlayerId()
        {
            if (_cachedSelfId != 0) return _cachedSelfId;

            var account = Sub(_fAccount);
            if (account == IntPtr.Zero || _fAccountPlayer == IntPtr.Zero || _fPlayerModelId == IntPtr.Zero)
                return 0;

            var player = Il2Cpp.FieldObject(account, _fAccountPlayer);
            if (player == IntPtr.Zero) return 0;

            _cachedSelfId = Il2Cpp.FieldValue<long>(player, _fPlayerModelId);
            return _cachedSelfId;
        }

        public long CurrentPlayerId()
        {
            var battle = Sub(_fBattle);
            if (battle == IntPtr.Zero || _fCurPlayerId == IntPtr.Zero) return 0;
            return Il2Cpp.FieldValue<long>(battle, _fCurPlayerId);
        }

        public int FightType()
        {
            var fight = Sub(_fFight);
            if (fight == IntPtr.Zero || _fFightType == IntPtr.Zero) return -1;
            return Il2Cpp.FieldValue<int>(fight, _fFightType);
        }

        private IntPtr FightWindow()
        {
            if (_fUiInst == IntPtr.Zero || _fFightWindow == IntPtr.Zero) return IntPtr.Zero;
            var ui = ReadSingleton(_fUiInst);
            return ui == IntPtr.Zero ? IntPtr.Zero : Il2Cpp.FieldObject(ui, _fFightWindow);
        }

        /// <summary>防御请求序号。非 0 表示防御窗口出现过；注意它不会归零，所以必须配合身份判断和值去重。</summary>
        public long DodgeSn()
        {
            var win = FightWindow();
            if (win == IntPtr.Zero || _fDodgeChoice == IntPtr.Zero) return 0;
            return Il2Cpp.FieldValue<long>(win, _fDodgeChoice);
        }

        /// <summary>本次战斗的防守方 playerId（读不到返回 0）。</summary>
        public long DefenderId()
        {
            var fight = Sub(_fFight);
            if (fight == IntPtr.Zero || _fFightData == IntPtr.Zero) return 0;
            var data = Il2Cpp.FieldObject(fight, _fFightData);
            if (data == IntPtr.Zero || _fDefenderField == IntPtr.Zero) return 0;
            var role = Il2Cpp.FieldObject(data, _fDefenderField);
            if (role == IntPtr.Zero || _fRolePlayerId == IntPtr.Zero) return 0;
            return Il2Cpp.FieldValue<long>(role, _fRolePlayerId);
        }

        private long DefenderIdViaWindow()
        {
            var win = FightWindow();
            if (win == IntPtr.Zero) return 0;
            if (_fDefenderData == IntPtr.Zero || _fBpdPlayer == IntPtr.Zero ||
                _fRoomPlayerServerPlayer == IntPtr.Zero || _fPlayerModelId == IntPtr.Zero) return 0;

            var bpd = Il2Cpp.FieldObject(win, _fDefenderData);
            if (bpd == IntPtr.Zero) return 0;
            var roomPlayer = Il2Cpp.FieldObject(bpd, _fBpdPlayer);
            if (roomPlayer == IntPtr.Zero) return 0;
            var serverPlayer = Il2Cpp.FieldObject(roomPlayer, _fRoomPlayerServerPlayer);
            if (serverPlayer == IntPtr.Zero) return 0;
            return Il2Cpp.FieldValue<long>(serverPlayer, _fPlayerModelId);
        }

        /// <summary>是否需要我按键防御：防御请求存在，且防守方就是本地玩家。</summary>
        public bool IsDefendingNow()
        {
            var sn = DodgeSn();
            if (sn == 0) return false;
            var defender = DefenderId();
            return defender != 0 && defender == SelfPlayerId();
        }

        /// <summary>调试：防御窗口真实状态。</summary>
        public string DebugFight()
        {
            if (_fFightWindow == IntPtr.Zero || _fDodgeChoice == IntPtr.Zero) return "无字段";
            var ui = ReadSingleton(_fUiInst);
            if (ui == IntPtr.Zero) return "无 UIManager";
            var win = Il2Cpp.FieldObject(ui, _fFightWindow);
            if (win == IntPtr.Zero) return "FightWindow 未创建";
            long dc = Il2Cpp.FieldValue<long>(win, _fDodgeChoice);
            return "fightType=" + FightType() + " dodgeChoice=" + dc;
        }

        private bool IsDefendingNowLegacy()
        {
            if (FightType() != 4) return false;
            var fight = Sub(_fFight);
            if (fight == IntPtr.Zero || _fFightData == IntPtr.Zero || _fDefenderField == IntPtr.Zero) return false;
            var data = Il2Cpp.FieldObject(fight, _fFightData);
            if (data == IntPtr.Zero || _fDefenderField == IntPtr.Zero) return false;
            var role = Il2Cpp.FieldObject(data, _fDefenderField);
            if (role == IntPtr.Zero || _fRolePlayerId == IntPtr.Zero) return false;
            var defenderId = Il2Cpp.FieldValue<long>(role, _fRolePlayerId);
            return defenderId != 0 && defenderId == SelfPlayerId();
        }

        /// <summary>读 protobuf RepeatedField 的元素个数（字段名 count）。</summary>
        private static int CollectionCount(IntPtr collection)
        {
            if (collection == IntPtr.Zero) return -1;
            var klass = Il2Cpp.ClassOf(collection);
            var f = Il2Cpp.Field(klass, "count");
            if (f == IntPtr.Zero) f = Il2Cpp.Field(klass, "_count");
            if (f == IntPtr.Zero) return -1;
            return Il2Cpp.FieldValue<int>(collection, f);
        }

        /// <summary>
        /// 扫描 UIManager 上所有名字含 "Window" 的字段，报告非 null 的那些。
        /// 这是最可靠的"当前有哪些窗口在活动"判定（不依赖 _unclearedWindows 这种临时列表）。
        /// </summary>
        /// <summary>
        /// 列出【正在显示】的窗口（FairyGUI 的 isShowing 等价于 parent != null）。
        /// 只读 UIManager 上类型为 *Window 的字段，跳过 Stack/List 这类容器字段。
        /// </summary>
        public string ShownWindowFields()
        {
            var ui = ReadSingleton(_fUiInst);
            if (ui == IntPtr.Zero) return "无 UIManager";

            var sb = new System.Text.StringBuilder();
            var iter = IntPtr.Zero;
            int guard = 0;
            while (guard++ < 400)
            {
                var field = Native.il2cpp_class_get_fields(_clsUiManager, ref iter);
                if (field == IntPtr.Zero) break;

                var name = Il2Cpp.NameOf(Native.il2cpp_field_get_name(field));
                if (name == null || !name.Contains("Window")) continue;
                if (name.StartsWith("propUp") || name.StartsWith("_uncleared")) continue;

                IntPtr win;
                try { win = Il2Cpp.FieldObject(ui, field); } catch { continue; }
                if (win == IntPtr.Zero) continue;

                if (IsWindowShown(win)) sb.Append(name).Append(' ');
            }
            return sb.Length == 0 ? "(无显示中窗口)" : sb.ToString();
        }

        /// <summary>权威防御信号所需的字段是否都解析成功（否则要退回 defenderInfo 判定）。</summary>
        public bool DefendSignalAvailable
            => _fContentPane != IntPtr.Zero && _fBtnDefend != IntPtr.Zero && _fVisible != IntPtr.Zero;

        /// <summary>
        /// 权威的「是否轮到我防御」信号：UIFightWindow.btn_Defend.visible。
        ///
        /// FightWindow.RefreshDefendReadyChoice 里只有 IsSelf(_playerId) 那个分支会把
        /// btn_Defend.visible 置为 true，别人防御时置为 false，两个分支互斥且在同一次
        /// 调用里完成，因此它不存在 battleFightData.defenderInfo 那种跨字段竞态。
        /// 它同时还隐含了「防御窗口已就绪」，比只看请求序号可靠。
        /// </summary>
        public bool BtnDefendVisible()
        {
            try
            {
                var win = FightWindow();
                if (win == IntPtr.Zero) return false;
                if (_fContentPane == IntPtr.Zero || _fBtnDefend == IntPtr.Zero || _fVisible == IntPtr.Zero)
                    return false;

                var pane = Il2Cpp.FieldObject(win, _fContentPane);
                if (pane == IntPtr.Zero) return false;

                var btn = Il2Cpp.FieldObject(pane, _fBtnDefend);
                if (btn == IntPtr.Zero) return false;

                return Il2Cpp.FieldValue<byte>(btn, _fVisible) != 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>调试：防御按钮与防守方的原始读数。</summary>
        public string DebugDefend()
        {
            return "btnDefend=" + BtnDefendVisible() +
                   " dodgeChoice=" + DodgeSn() +
                   " defender=" + DefenderId() +
                   " defenderWin=" + DefenderIdViaWindow() +
                   " fightType=" + FightType();
        }
        /// <summary>FairyGUI.Window.isShowing 等价于 parent != null。</summary>
        private bool IsWindowShown(IntPtr win)
        {
            if (win == IntPtr.Zero || _fParentBacking == IntPtr.Zero) return false;
            try { return Il2Cpp.FieldObject(win, _fParentBacking) != IntPtr.Zero; }
            catch { return false; }
        }

        /// <summary>指定窗口字段当前是否正在显示。</summary>
        public bool IsShown(string fieldName)
        {
            var ui = ReadSingleton(_fUiInst);
            if (ui == IntPtr.Zero) return false;
            var field = Il2Cpp.Field(_clsUiManager, fieldName);
            if (field == IntPtr.Zero) return false;
            var win = Il2Cpp.FieldObject(ui, field);
            return win != IntPtr.Zero && IsWindowShown(win);
        }

        /// <summary>列出当前 UIManager 记录的所有活动窗口类型名（诊断用）。</summary>
        public string ActiveWindowTypes()
        {
            var ui = ReadSingleton(_fUiInst);
            if (ui == IntPtr.Zero) return "无 UIManager";

            var names = new System.Text.StringBuilder();

            var list = _fUnclearedWindows == IntPtr.Zero ? IntPtr.Zero : Il2Cpp.FieldObject(ui, _fUnclearedWindows);
            if (list != IntPtr.Zero)
            {
                int n = ListCount(list);
                for (int i = 0; i < n && i < 40; i++)
                {
                    var item = ListItem(list, i);
                    if (item == IntPtr.Zero) continue;
                    names.Append(Il2Cpp.ClassName(Il2Cpp.ClassOf(item))).Append(' ');
                }
            }
            return names.Length == 0 ? "(空)" : names.ToString();
        }

        /// <summary>是否存在指定类型的活动窗口。</summary>
        public bool HasActiveWindow(string typeName)
        {
            var ui = ReadSingleton(_fUiInst);
            if (ui == IntPtr.Zero) return false;

            var list = _fUnclearedWindows == IntPtr.Zero ? IntPtr.Zero : Il2Cpp.FieldObject(ui, _fUnclearedWindows);
            if (list == IntPtr.Zero) return false;

            int n = ListCount(list);
            for (int i = 0; i < n && i < 60; i++)
            {
                var item = ListItem(list, i);
                if (item == IntPtr.Zero) continue;
                if (Il2Cpp.ClassName(Il2Cpp.ClassOf(item)) == typeName) return true;
            }
            return false;
        }

        private static int ListCount(IntPtr list)
        {
            // List<T> 的 _size 字段就是元素个数
            var klass = Il2Cpp.ClassOf(list);
            var sizeField = Il2Cpp.Field(klass, "_size");
            if (sizeField == IntPtr.Zero) return 0;
            return Il2Cpp.FieldValue<int>(list, sizeField);
        }

        private static IntPtr ListItem(IntPtr list, int index)
        {
            var klass = Il2Cpp.ClassOf(list);
            var itemsField = Il2Cpp.Field(klass, "_items");
            if (itemsField == IntPtr.Zero) return IntPtr.Zero;
            var arr = Il2Cpp.FieldObject(list, itemsField);
            if (arr == IntPtr.Zero) return IntPtr.Zero;
            return Il2Cpp.ArrayGet(arr, index);
        }

        public struct RoundCardDebug
        {
            public IntPtr UiInst;
            public IntPtr Win;
            public long Sn;
            public bool Visible;
            public int CardCount;
        }

        /// <summary>调试：把回合卡窗口的真实状态全部读出来。</summary>
        public RoundCardDebug DebugRoundCard()
        {
            var d = new RoundCardDebug();
            d.UiInst = _fUiInst == IntPtr.Zero ? IntPtr.Zero : ReadSingleton(_fUiInst);
            if (d.UiInst == IntPtr.Zero) return d;

            if (_fRoundCardWindow != IntPtr.Zero)
            {
                d.Win = Il2Cpp.FieldObject(d.UiInst, _fRoundCardWindow);
                if (d.Win != IntPtr.Zero)
                {
                    if (_fRoundCardSn != IntPtr.Zero) d.Sn = Il2Cpp.FieldValue<long>(d.Win, _fRoundCardSn);
                    if (_fRoundCardIds != IntPtr.Zero)
                    {
                        var ids = Il2Cpp.FieldObject(d.Win, _fRoundCardIds);
                        if (ids != IntPtr.Zero) d.CardCount = CollectionCount(ids);
                    }
                    try { d.Visible = Il2Cpp.FieldValue<byte>(d.Win, _fVisible) != 0; } catch { }
                }
            }
            return d;
        }

        /// <summary>
        /// 是否轮到我选"回合奖励卡"。
        /// 实测游戏里该界面是 RelicWindow（遗物三选一），不是 ChooseRoundCardWindow。
        /// RelicLogic.DealRelic 里只有 IsSelf(action.PlayerId) 才会调 ShowRelicData 显示此窗口，
        /// 别人选遗物走的是气泡提示（ShowMultiplePlayerThink），所以"窗口在显示"本身就等价于"轮到我了"。
        /// </summary>
        public bool IsChoosingRoundCardNow()
            => IsShown("_cachedRelicWindow");
    }
}



