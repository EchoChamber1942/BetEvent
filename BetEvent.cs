using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("BetEvent", "EchoChamber", "1.0.0")]
    [Description("Configurable betting event system with player UI, admin UI, scheduling, and multilingual support.")]
    class BetEvent : RustPlugin
    {
        private const int SCRAP_ID = -932201673;
        private const string UiName = "BetEventUI";
        private const string AdminUiName = "BetEventAdminUI";
        private const int MaxOptions = 12;
        private readonly int[] buttonAmounts = { 100, 500, 1000, 5000 };

        class BetData
        {
            public Dictionary<int, int> Bets = new Dictionary<int, int>();
        }

        class StoredData
        {
            public Dictionary<ulong, int> PendingScrap = new Dictionary<ulong, int>();
            public Dictionary<ulong, BetData> PlayerBets = new Dictionary<ulong, BetData>();
            public Dictionary<int, int> TotalPool = new Dictionary<int, int>();
            public List<string> OptionLabels = new List<string>();
            public int OptionCount = 6;
            public bool IsOpen = false;
            public bool HasEndTime = false;
            public long EndTimeTicks = 0;
            public List<string> AnnouncedMilestones = new List<string>();

            // Start予約
            public bool HasStartTime;
            public long StartTimeTicks;

            // Admin Preserving UI Input
            public string DraftStartDate;
            public string DraftStartTime;
            public string DraftEndDate;
            public string DraftEndTime;
        }

        private StoredData storedData;
        private Dictionary<ulong, int> pendingScrap = new Dictionary<ulong, int>();
        private Dictionary<ulong, BetData> playerBets = new Dictionary<ulong, BetData>();
        private Dictionary<int, int> totalPool = new Dictionary<int, int>();
        private HashSet<ulong> openedUiPlayers = new HashSet<ulong>();
        private HashSet<ulong> openedAdminUiPlayers = new HashSet<ulong>();
        private HashSet<string> announcedMilestones = new HashSet<string>();

        private bool isOpen = false;
        private bool hasEndTime = false;
        private DateTime endTime = DateTime.MinValue;

        // Start Reservation
        private bool hasStartTime = false;
        private DateTime startTime = DateTime.MinValue;

        // Admin Preserving UI Input
        private string draftStartDate = "";
        private string draftStartTime = "";
        private string draftEndDate = "";
        private string draftEndTime = "";
        private Timer monitorTimer;

        private int optionCount = 6;
        private List<string> optionLabels = new List<string> { "A", "B", "C", "D", "E", "F" };

        private Dictionary<string, string> englishMessages = new Dictionary<string, string>();
        private Dictionary<string, string> japaneseMessages = new Dictionary<string, string>();

        private Dictionary<string, string> GetEnglishLanguageTemplate()
        {
            return new Dictionary<string, string>
            {
                ["plugin_title"] = "BetEvent",
                ["player_ui_title"] = "BetEvent",
                ["admin_ui_title"] = "BetEvent Admin",
                ["status_waiting"] = "Waiting to Start",
                ["status_open"] = "Open",
                ["status_closed"] = "Closed",
                ["start_time"] = "Start time",
                ["end_time"] = "Close time",
                ["not_set"] = "Not set",
                ["time_until_close"] = "Time until close",
                ["refresh_hint"] = "Use Refresh to show the latest state",
                ["total_pool"] = "Total pool",
                ["participants"] = "Participants",
                ["your_bets"] = "Your bets",
                ["participant_list"] = "Participants",
                ["bet_waiting"] = "Waiting for bets",
                ["popular_1"] = "Top 1",
                ["popular_2"] = "Top 2",
                ["popular_3"] = "Top 3",
                ["you_bet"] = "You",
                ["refresh"] = "Refresh",
                ["close"] = "Close",
                ["admin_ui"] = "Admin UI",
                ["admin_status"] = "Status",
                ["entry_actions"] = "Entry Controls",
                ["open_now"] = "Open Now",
                ["close_entries"] = "Close Entries",
                ["refund_all"] = "Refund All",
                ["reset"] = "Reset",
                ["option_settings"] = "Option Count (only when no bets remain)",
                ["settle_result"] = "Result Finalized",
                ["schedule_settings"] = "Schedule (YYYY-MM-DD / HH:mm)",
                ["start"] = "Start",
                ["end"] = "End",
                ["date"] = "Date",
                ["time"] = "Time",
                ["schedule_note"] = "Note: change labels with /beteventcfg labels Red,Blue,Green",
                ["apply_schedule"] = "Apply Schedule",
                ["clear_schedule"] = "Clear Schedule",
                ["remaining"] = "Remaining",
                ["scheduled_start"] = "Scheduled start",
                ["scheduled_close"] = "Scheduled close",
                ["options"] = "Options",
                ["labels"] = "Labels",
                ["current"] = "Current",
                ["option_prefix"] = "Option",
                ["approx"] = "Approx.",
                ["waiting_for_bets"] = "Waiting for bets",
                ["no_bets"] = "No bets",
                ["and_more"] = "...and {0} more",
                ["starts_in"] = "Starts in",
                ["ended"] = "Ended",
                                ["closes"] = "Closes",
                ["msg_pending_returned"] = "Returned {0} scrap from pending rewards.\n",
                ["msg_schedule_locked"] = "You cannot change the schedule while betting is open or bets remain. Run refund or reset first.\n",
                ["msg_schedule_applied"] = "Schedule set. Starts {0:MM/dd HH:mm} / Closes {1:MM/dd HH:mm}",
                ["msg_betting_closed"] = "Betting has been closed!",
                ["msg_refunded_all"] = "Refunded all bets.",
                ["msg_reset_done"] = "Bet data has been reset. Refunds have also been completed.",
                ["msg_option_locked"] = "You cannot change the option count while betting is open or bets remain. Run refund or reset first.\n",
                ["msg_invalid_command"] = "Invalid bet command.\n",
                ["msg_admin_only"] = "Admin only.\n",
                ["msg_open_until"] = "Betting is now open. Closes at {0:HH:mm}.",
                ["msg_no_close_time"] = "No close time is currently set.\n",
                ["msg_currently_closed"] = "Betting is currently closed.\n",
                ["msg_close_time_remaining"] = "Close time: {0:HH:mm} / Remaining: {1}\n",
                ["msg_usage_result"] = "Usage: /bet result A\n",
                ["msg_label_or_number"] = "Specify a configured label or a number between 1 and the option count.\n",
                ["msg_usage_bet"] = "Usage: /bet A 100\n",
                ["msg_bet_amount_min"] = "Bet amount must be 1 or higher.\n",
                ["msg_not_enough_scrap"] = "Not enough scrap.\n",
                ["msg_placed_bet"] = "Placed {0} scrap on option {1}.\n",
                ["msg_usage_cfg"] = "Usage: /beteventcfg options 4  or  /beteventcfg labels Red,Blue,Green,Yellow\n(Option count: 1-12)",
                ["msg_usage_cfg_options"] = "Usage: /beteventcfg options 4\n(Option count: 1-12)",
                ["msg_option_range"] = "Option count must be between 1 and 12.\n",
                ["msg_option_set"] = "Option count set to {0}.\n",
                ["msg_usage_cfg_labels"] = "Usage: /beteventcfg labels Red,Blue,Green,Yellow\n(Label count: 1-12)",
                ["msg_labels_locked"] = "You cannot change labels while betting is open or bets remain. Run refund or reset first.\n",
                ["msg_label_count_range"] = "Label count must be between 1 and 12.\n",
                ["msg_labels_updated"] = "Labels updated: {0}\n",
                ["msg_auto_closed"] = "Betting was closed automatically!",
                ["headline_auto_close"] = "Auto Close",
                ["detail_auto_close"] = "The entry period has ended",
                ["overlay_closed_auto"] = "BET CLOSED\nClosed automatically",
                ["msg_minutes_left_with_time"] = "{0} minutes left until betting closes! Close time: {1:HH:mm}",
                ["msg_minutes_left"] = "{0} minutes left until betting closes!",
                ["msg_30_seconds_left"] = "30 seconds left until betting closes!",
                ["overlay_final_30"] = "BET FINAL 30s\n30 seconds remaining",
                ["msg_no_bets_result_cancelled"] = "No bets have been placed. Result processing was cancelled.",
                ["msg_no_winners"] = "There were no winners on option {0}. Run /bet refund if needed.",
                ["headline_result_finalized"] = "Result Finalized",
                ["detail_result_no_winner"] = "Option {0} / No winners",
                ["overlay_result_no_winner"] = "RESULT\nOption {0}: no winners",
                ["msg_congrats_won"] = "Congratulations! You won {0} scrap on option {1}.\n",
                ["headline_result_winner"] = "🎉 Result Finalized!",
                ["detail_result_winner"] = "Winning option: {0}!",
                ["chat_result_winner"] = "🎉 Result Finalized! Winning option: {0}!",
                ["chat_total_pool_winners"] = "Total pool: {0} scrap / Winners: {1}",
                ["overlay_closed_manual"] = "BET CLOSED\nEntries are closed",
                ["msg_schedule_open_until"] = "Scheduled betting is now open. Closes at {0:MM/dd HH:mm}.",
                ["headline_schedule_opened"] = "Scheduled betting opened!",
                ["detail_closes_at_full"] = "Closes {0:MM/dd HH:mm}",
                ["headline_betting_opened"] = "Betting opened!",
                ["detail_closes_at_short"] = "Closes {0:HH:mm}",
                ["msg_betting_open"] = "Betting is now open.",
                ["detail_no_close_time"] = "No close time set"
            };
        }

        private void LoadLanguageCache()
        {
            englishMessages = GetEnglishLanguageTemplate();
            japaneseMessages = GetJapaneseLanguageTemplate();

            try
            {
                var langDir = Path.GetFullPath(Path.Combine(Interface.Oxide.DataDirectory, "..", "lang", "ja"));
                var langPath = Path.Combine(langDir, $"{Name}.json");
                if (File.Exists(langPath))
                {
                    var json = File.ReadAllText(langPath, Encoding.UTF8);
                    var loaded = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (loaded != null)
                    {
                        foreach (var pair in loaded)
                            japaneseMessages[pair.Key] = pair.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                PrintWarning("Failed to load Japanese language file: " + ex.Message);
            }
        }

        private string T(string key, BasePlayer player = null)
        {
            string code = null;
            if (player != null)
            {
                try
                {
                    code = lang.GetLanguage(player.UserIDString);
                }
                catch
                {
                    code = null;
                }
            }

            bool useJapanese = !string.IsNullOrEmpty(code) && code.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
            var primary = useJapanese ? japaneseMessages : englishMessages;
            var fallback = useJapanese ? englishMessages : japaneseMessages;

            string value;
            if (primary != null && primary.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value))
                return value;
            if (fallback != null && fallback.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value))
                return value;
            return key;
        }

        private Dictionary<string, string> GetJapaneseLanguageTemplate()
        {
            return new Dictionary<string, string>
            {
                ["plugin_title"] = "BetEvent",
                ["player_ui_title"] = "BetEvent",
                ["admin_ui_title"] = "BetEvent 管理",
                ["status_waiting"] = "開始待ち",
                ["status_open"] = "受付中",
                ["status_closed"] = "受付終了",
                ["start_time"] = "開始時刻",
                ["end_time"] = "締切時刻",
                ["not_set"] = "未設定",
                ["time_until_close"] = "受付終了まで",
                ["refresh_hint"] = "最新状況は『更新』ボタンで反映されます",
                ["total_pool"] = "総ベット額",
                ["participants"] = "参加人数",
                ["your_bets"] = "あなたの賭け",
                ["participant_list"] = "参加者一覧",
                ["bet_waiting"] = "ベット待ち",
                ["popular_1"] = "人気1位",
                ["popular_2"] = "人気2位",
                ["popular_3"] = "人気3位",
                ["you_bet"] = "あなた",
                ["refresh"] = "更新",
                ["close"] = "閉じる",
                ["admin_ui"] = "管理UI",
                ["admin_status"] = "状態",
                ["entry_actions"] = "受付操作",
                ["open_now"] = "即時開始",
                ["close_entries"] = "受付締切",
                ["refund_all"] = "全額返金",
                ["reset"] = "リセット",
                ["option_settings"] = "枠数設定（ベット残存なし時のみ）",
                ["settle_result"] = "結果確定",
                ["schedule_settings"] = "予約設定（YYYY-MM-DD / HH:mm）",
                ["start"] = "開始",
                ["end"] = "終了",
                ["date"] = "日付",
                ["time"] = "時刻",
                ["schedule_note"] = "メモ: ラベル変更は /beteventcfg labels 赤,青,緑 の形式で使用",
                ["apply_schedule"] = "予約適用",
                ["clear_schedule"] = "予約クリア",
                ["remaining"] = "残り時間",
                ["scheduled_start"] = "予約開始",
                ["scheduled_close"] = "予約終了",
                ["options"] = "枠数",
                ["labels"] = "ラベル",
                ["current"] = "現在",
                ["option_prefix"] = "枠",
                ["approx"] = "概算",
                ["waiting_for_bets"] = "ベット待ち",
                ["no_bets"] = "賭けなし",
                ["and_more"] = "…ほか {0} 名",
                ["starts_in"] = "開始まで",
                ["ended"] = "終了",
                                ["closes"] = "締切",
                ["msg_pending_returned"] = "保留報酬としてスクラップ {0} を返却しました。\n",
                ["msg_schedule_locked"] = "受付中またはベットが残っている間は予約を変更できません。先に返金またはリセットを実行してください。\n",
                ["msg_schedule_applied"] = "予約を設定しました。開始 {0:MM/dd HH:mm} / 締切 {1:MM/dd HH:mm}",
                ["msg_betting_closed"] = "ベット受付を締め切りました！",
                ["msg_refunded_all"] = "すべてのベットを返金しました。",
                ["msg_reset_done"] = "ベットデータをリセットし、返金も完了しました。",
                ["msg_option_locked"] = "受付中またはベットが残っている間は枠数を変更できません。先に返金またはリセットを実行してください。\n",
                ["msg_invalid_command"] = "ベットコマンドが不正です。\n",
                ["msg_admin_only"] = "管理者専用です。\n",
                ["msg_open_until"] = "ベット受付を開始しました。締切 {0:HH:mm}",
                ["msg_no_close_time"] = "現在、締切時刻は設定されていません。\n",
                ["msg_currently_closed"] = "現在、ベット受付は終了しています。\n",
                ["msg_close_time_remaining"] = "締切時刻: {0:HH:mm} / 残り: {1}\n",
                ["msg_usage_result"] = "使用法: /bet result A\n",
                ["msg_label_or_number"] = "設定済みラベル、または 1 から枠数までの番号を指定してください。\n",
                ["msg_usage_bet"] = "使用法: /bet A 100\n",
                ["msg_bet_amount_min"] = "ベット額は 1 以上である必要があります。\n",
                ["msg_not_enough_scrap"] = "スクラップが不足しています。\n",
                ["msg_placed_bet"] = "枠 {1} にスクラップ {0} をベットしました。\n",
                ["msg_usage_cfg"] = "使用法: /beteventcfg options 4  または  /beteventcfg labels 赤,青,緑,黄\n(枠数: 1-12)",
                ["msg_usage_cfg_options"] = "使用法: /beteventcfg options 4\n(枠数: 1-12)",
                ["msg_option_range"] = "枠数は 1 から 12 の間で指定してください。\n",
                ["msg_option_set"] = "枠数を {0} に設定しました。\n",
                ["msg_usage_cfg_labels"] = "使用法: /beteventcfg labels 赤,青,緑,黄\n(ラベル数: 1-12)",
                ["msg_labels_locked"] = "受付中またはベットが残っている間はラベルを変更できません。先に返金またはリセットを実行してください。\n",
                ["msg_label_count_range"] = "ラベル数は 1 から 12 の間で指定してください。\n",
                ["msg_labels_updated"] = "ラベルを更新しました: {0}\n",
                ["msg_auto_closed"] = "ベット受付を自動で締め切りました！",
                ["headline_auto_close"] = "自動締切",
                ["detail_auto_close"] = "受付時間が終了しました",
                ["overlay_closed_auto"] = "BET CLOSED\n自動で締切",
                ["msg_minutes_left_with_time"] = "ベット締切まであと {0} 分！ 締切 {1:HH:mm}",
                ["msg_minutes_left"] = "ベット締切まであと {0} 分！",
                ["msg_30_seconds_left"] = "ベット締切まであと 30 秒！",
                ["overlay_final_30"] = "BET FINAL 30s\n残り30秒",
                ["msg_no_bets_result_cancelled"] = "ベットが1件も無かったため、結果確定を中止しました。",
                ["msg_no_winners"] = "枠 {0} に勝者はいませんでした。必要であれば /bet refund を実行してください。",
                ["headline_result_finalized"] = "結果確定",
                ["detail_result_no_winner"] = "枠 {0} / 勝者なし",
                ["overlay_result_no_winner"] = "RESULT\n枠 {0}: 勝者なし",
                ["msg_congrats_won"] = "おめでとうございます！ 枠 {1} でスクラップ {0} を獲得しました。\n",
                ["headline_result_winner"] = "🎉 結果確定！",
                ["detail_result_winner"] = "当選枠: {0}！",
                ["chat_result_winner"] = "🎉 結果確定！ 当選枠: {0}！",
                ["chat_total_pool_winners"] = "総ベット額: {0} スクラップ / 勝者数: {1}",
                ["overlay_closed_manual"] = "BET CLOSED\n受付を締め切りました",
                ["msg_schedule_open_until"] = "予約ベットを開始しました。締切 {0:MM/dd HH:mm}",
                ["headline_schedule_opened"] = "予約ベット開始",
                ["detail_closes_at_full"] = "締切 {0:MM/dd HH:mm}",
                ["headline_betting_opened"] = "ベット受付開始",
                ["detail_closes_at_short"] = "締切 {0:HH:mm}",
                ["msg_betting_open"] = "ベット受付を開始しました。",
                ["detail_no_close_time"] = "締切時刻は未設定"
            };
        }

        private void EnsureJapaneseLanguageTemplate()
        {
            try
            {
                var langDir = Path.GetFullPath(Path.Combine(Interface.Oxide.DataDirectory, "..", "lang", "ja"));
                Directory.CreateDirectory(langDir);
                var langPath = Path.Combine(langDir, $"{Name}.json");
                if (!File.Exists(langPath))
                    File.WriteAllText(langPath, JsonConvert.SerializeObject(GetJapaneseLanguageTemplate(), Formatting.Indented), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                PrintWarning("Failed to create Japanese language template: " + ex.Message);
            }
        }

        private string streamOutputDir;
        private string overlayPath;
        private string triggerPath;

        void Init()
        {
            EnsureStreamPaths();
            EnsureJapaneseLanguageTemplate();
            LoadLanguageCache();
            LoadData();
            RestoreStateAfterLoad();
            StartTimers();
            WriteStreamStateAfterLoad();
        }

        void OnServerInitialized()
        {
            EnsureStreamPaths();
            LoadLanguageCache();
            StartTimers();
            CheckAutoClose();
            WriteStreamStateAfterLoad();
        }

        void EnsureStreamPaths()
        {
            streamOutputDir = Path.Combine(Interface.Oxide.DataDirectory, Name);
            overlayPath = Path.Combine(streamOutputDir, "betevent_overlay.txt");
            triggerPath = Path.Combine(streamOutputDir, "betevent_trigger.json");
            Directory.CreateDirectory(streamOutputDir);
        }

        void NormalizeOptions()
        {
            if (optionCount < 1) optionCount = 1;
            if (optionCount > MaxOptions) optionCount = MaxOptions;

            if (optionLabels == null)
                optionLabels = new List<string>();

            while (optionLabels.Count < optionCount)
            {
                optionLabels.Add(GetDefaultLabel(optionLabels.Count));
            }

            if (optionLabels.Count > optionCount)
                optionLabels.RemoveRange(optionCount, optionLabels.Count - optionCount);

            for (int i = 0; i < optionLabels.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(optionLabels[i]))
                    optionLabels[i] = GetDefaultLabel(i);
            }
        }

        string GetDefaultLabel(int index)
        {
            if (index < 26)
                return ((char)('A' + index)).ToString();

            int first = index / 26 - 1;
            int second = index % 26;
            return ((char)('A' + first)).ToString() + ((char)('A' + second)).ToString();
        }

        void LoadData()
        {
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch
            {
                storedData = new StoredData();
            }

            if (storedData == null)
                storedData = new StoredData();

            pendingScrap = storedData.PendingScrap ?? new Dictionary<ulong, int>();
            playerBets = storedData.PlayerBets ?? new Dictionary<ulong, BetData>();
            totalPool = storedData.TotalPool ?? new Dictionary<int, int>();
            optionLabels = storedData.OptionLabels ?? new List<string>();
            optionCount = storedData.OptionCount;
            if (optionCount < 1 || optionCount > MaxOptions)
                optionCount = 6;
            isOpen = storedData.IsOpen;
            hasEndTime = storedData.HasEndTime;
            endTime = storedData.EndTimeTicks > 0 ? new DateTime(storedData.EndTimeTicks, DateTimeKind.Local) : DateTime.MinValue;
            announcedMilestones = new HashSet<string>(storedData.AnnouncedMilestones ?? new List<string>());

            hasStartTime = storedData.HasStartTime;
            startTime = hasStartTime && storedData.StartTimeTicks > 0
                ? new DateTime(storedData.StartTimeTicks, DateTimeKind.Local)
                : DateTime.MinValue;
            if (startTime == DateTime.MinValue)
                hasStartTime = false;

            draftStartDate = storedData.DraftStartDate ?? "";
            draftStartTime = storedData.DraftStartTime ?? "";
            draftEndDate = storedData.DraftEndDate ?? "";
            draftEndTime = storedData.DraftEndTime ?? "";
        }

        void SaveData()
        {
            if (storedData == null)
                storedData = new StoredData();

            storedData.PendingScrap = pendingScrap ?? new Dictionary<ulong, int>();
            storedData.PlayerBets = playerBets ?? new Dictionary<ulong, BetData>();
            storedData.TotalPool = totalPool ?? new Dictionary<int, int>();
            storedData.OptionLabels = optionLabels ?? new List<string>();
            storedData.OptionCount = optionCount;
            storedData.IsOpen = isOpen;
            storedData.HasEndTime = hasEndTime;
            storedData.EndTimeTicks = hasEndTime && endTime != DateTime.MinValue ? endTime.Ticks : 0;
            storedData.AnnouncedMilestones = new List<string>(announcedMilestones ?? new HashSet<string>());
            storedData.HasStartTime = hasStartTime;
            storedData.StartTimeTicks = hasStartTime && startTime != DateTime.MinValue ? startTime.Ticks : 0;
            storedData.DraftStartDate = draftStartDate ?? "";
            storedData.DraftStartTime = draftStartTime ?? "";
            storedData.DraftEndDate = draftEndDate ?? "";
            storedData.DraftEndTime = draftEndTime ?? "";
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }

        void RestoreStateAfterLoad()
        {
            NormalizeOptions();

            if (pendingScrap == null)
                pendingScrap = new Dictionary<ulong, int>();

            if (playerBets == null)
                playerBets = new Dictionary<ulong, BetData>();

            if (totalPool == null)
                totalPool = new Dictionary<int, int>();

            var normalizedPool = new Dictionary<int, int>();
            for (int i = 1; i <= optionCount; i++)
            {
                int amount;
                normalizedPool[i] = totalPool.TryGetValue(i, out amount) && amount > 0 ? amount : 0;
            }
            totalPool = normalizedPool;

            var normalizedBets = new Dictionary<ulong, BetData>();
            foreach (var entry in playerBets)
            {
                if (entry.Value == null || entry.Value.Bets == null)
                    continue;

                var clean = new BetData();
                foreach (var bet in entry.Value.Bets)
                {
                    if (bet.Key < 1 || bet.Key > optionCount) continue;
                    if (bet.Value <= 0) continue;
                    clean.Bets[bet.Key] = bet.Value;
                }

                if (clean.Bets.Count > 0)
                    normalizedBets[entry.Key] = clean;
            }
            playerBets = normalizedBets;

            RebuildTotalPoolFromBets();

            if (!hasEndTime)
                endTime = DateTime.MinValue;

            if (hasEndTime && endTime == DateTime.MinValue)
                hasEndTime = false;

            var now = DateTime.Now;

            if (hasStartTime && startTime == DateTime.MinValue)
                hasStartTime = false;

            if (hasStartTime && hasEndTime)
            {
                if (now >= endTime)
                {
                    isOpen = false;
                    hasStartTime = false;
                    hasEndTime = false;
                    startTime = DateTime.MinValue;
                    endTime = DateTime.MinValue;
                    announcedMilestones.Clear();
                    LogToFile("BetEvent", $"[RECOVERY CLOSE] {DateTime.Now:yyyy-MM-dd HH:mm:ss} Closed on recovery because both start and end times had already passed", this);
                }
                else if (now >= startTime)
                {
                    isOpen = true;
                    hasStartTime = false;
                    startTime = DateTime.MinValue;
                }
                else
                {
                    isOpen = false;
                }
            }
            else if (hasEndTime && now >= endTime)
            {
                isOpen = false;
                hasEndTime = false;
                endTime = DateTime.MinValue;
                announcedMilestones.Clear();
                LogToFile("BetEvent", $"[RECOVERY CLOSE] {DateTime.Now:yyyy-MM-dd HH:mm:ss} Closed on recovery because the end time had already passed", this);
            }

            SaveData();
        }

        void RebuildTotalPoolFromBets()
        {
            ResetPools();

            foreach (var entry in playerBets)
            {
                if (entry.Value == null || entry.Value.Bets == null)
                    continue;

                foreach (var bet in entry.Value.Bets)
                {
                    if (bet.Key < 1 || bet.Key > optionCount) continue;
                    if (bet.Value <= 0) continue;
                    totalPool[bet.Key] += bet.Value;
                }
            }
        }

        void WriteStreamStateAfterLoad()
        {
            if (isOpen || GetTotalPoolAmount() > 0)
                WriteLiveOverlay();
            else
                WriteIdleStreamFiles();
        }

        bool QueueScrapOrGive(ulong userId, int amount)
        {
            if (amount <= 0) return false;

            var player = BasePlayer.FindByID(userId);
            if (player != null && player.inventory != null)
            {
                var item = ItemManager.CreateByItemID(SCRAP_ID, amount);
                if (item == null) return false;
                player.GiveItem(item);
                return true;
            }

            int current;
            pendingScrap.TryGetValue(userId, out current);
            pendingScrap[userId] = current + amount;
            SaveData();
            return false;
        }

        void TryDeliverPendingScrap(BasePlayer player)
        {
            if (player == null || player.inventory == null) return;

            int amount;
            if (!pendingScrap.TryGetValue(player.userID, out amount) || amount <= 0) return;

            var item = ItemManager.CreateByItemID(SCRAP_ID, amount);
            if (item == null) return;

            player.GiveItem(item);
            pendingScrap.Remove(player.userID);
            SaveData();
            SendReply(player, string.Format(T("msg_pending_returned", player), amount));
            LogToFile("BetEvent", $"[PENDING CLAIM] {DateTime.Now:yyyy-MM-dd HH:mm:ss} player={player.displayName}({player.userID}) amount={amount}", this);
        }

        void StartTimers()
        {
            if (monitorTimer != null) return;

            monitorTimer = timer.Every(1f, () =>
            {
                if (hasStartTime && !isOpen && startTime != DateTime.MinValue && DateTime.Now >= startTime)
                {
                    StartScheduledBet();
                }

                if (!isOpen || !hasEndTime) return;

                var remaining = endTime - DateTime.Now;
                HandleCountdownAnnouncements(remaining);
                WriteLiveOverlay();

                if (remaining.TotalSeconds <= 0)
                    CloseBetAutomatically();
            });
        }



        bool TryParseDatePart(string dateText, string fieldName, out DateTime datePart, out string error)
        {
            datePart = DateTime.MinValue;
            error = null;
            dateText = (dateText ?? "").Trim();

            if (!DateTime.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out datePart))
            {
                error = string.Format("Invalid {0} format. Use YYYY-MM-DD.", fieldName);
                return false;
            }

            return true;
        }

        bool TryParseTimePart(string timeText, string fieldName, out DateTime timePart, out string error)
        {
            timePart = DateTime.MinValue;
            error = null;
            timeText = (timeText ?? "").Trim();

            if (!DateTime.TryParseExact(timeText, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out timePart))
            {
                error = string.Format("Invalid {0} format. Use HH:mm.", fieldName);
                return false;
            }

            return true;
        }

        bool TryApplySchedule(string startDateText, string startTimeText, string endDateText, string endTimeText, out string error)
        {
            error = null;

            startDateText = (startDateText ?? "").Trim();
            startTimeText = (startTimeText ?? "").Trim();
            endDateText = (endDateText ?? "").Trim();
            endTimeText = (endTimeText ?? "").Trim();

            DateTime now = DateTime.Now;
            DateTime startDatePart;
            if (string.IsNullOrEmpty(startDateText))
            {
                startDatePart = now.Date;
                draftStartDate = startDatePart.ToString("yyyy-MM-dd");
            }
            else if (!TryParseDatePart(startDateText, "StartDate", out startDatePart, out error))
            {
                return false;
            }

            if (string.IsNullOrEmpty(startTimeText))
            {
                error = "Enter a start time in HH:mm format.";
                return false;
            }

            DateTime startTimePart;
            if (!TryParseTimePart(startTimeText, "StartTime", out startTimePart, out error))
                return false;

            DateTime parsedStart = new DateTime(startDatePart.Year, startDatePart.Month, startDatePart.Day, startTimePart.Hour, startTimePart.Minute, 0, DateTimeKind.Local);

            DateTime parsedEnd;
            if (string.IsNullOrEmpty(endDateText) && string.IsNullOrEmpty(endTimeText))
            {
                parsedEnd = parsedStart.AddMinutes(30);
                draftEndDate = parsedEnd.ToString("yyyy-MM-dd");
                draftEndTime = parsedEnd.ToString("HH:mm");
            }
            else
            {
                DateTime endDatePart;
                if (string.IsNullOrEmpty(endDateText))
                {
                    endDatePart = startDatePart;
                    draftEndDate = endDatePart.ToString("yyyy-MM-dd");
                }
                else if (!TryParseDatePart(endDateText, "EndedDate", out endDatePart, out error))
                {
                    return false;
                }

                DateTime endTimePart;
                if (string.IsNullOrEmpty(endTimeText))
                {
                    parsedEnd = parsedStart.AddMinutes(30);
                    if (string.IsNullOrEmpty(endDateText))
                        draftEndDate = parsedEnd.ToString("yyyy-MM-dd");
                    else
                        draftEndDate = endDatePart.ToString("yyyy-MM-dd");
                    draftEndTime = parsedEnd.ToString("HH:mm");
                }
                else
                {
                    if (!TryParseTimePart(endTimeText, "EndedTime", out endTimePart, out error))
                        return false;

                    parsedEnd = new DateTime(endDatePart.Year, endDatePart.Month, endDatePart.Day, endTimePart.Hour, endTimePart.Minute, 0, DateTimeKind.Local);
                }
            }

            if (parsedEnd <= parsedStart)
            {
                error = "End time must be after the start time.";
                return false;
            }

            if (parsedEnd <= now)
            {
                error = "End time must be later than the current time.";
                return false;
            }

            startTime = parsedStart;
            endTime = parsedEnd;
            hasStartTime = true;
            hasEndTime = true;
            isOpen = false;
            draftStartDate = parsedStart.ToString("yyyy-MM-dd");
            draftStartTime = parsedStart.ToString("HH:mm");
            draftEndDate = parsedEnd.ToString("yyyy-MM-dd");
            draftEndTime = parsedEnd.ToString("HH:mm");
            announcedMilestones.Clear();
            SaveData();
            return true;
        }

        void StartScheduledBet()
        {
            if (!hasStartTime)
                return;

            isOpen = true;
            hasStartTime = false;
            startTime = DateTime.MinValue;
            announcedMilestones.Clear();

            BroadcastMessage(string.Format(T("msg_schedule_open_until"), endTime));
            BroadcastHeadline(T("headline_schedule_opened"), string.Format(T("detail_closes_at_full"), endTime));
            SaveData();
            WriteLiveOverlay();
            WriteTriggerJson("open", "", GetTotalPoolAmount(), new List<string>(), hasEndTime ? Math.Max(0, (int)Math.Ceiling((endTime - DateTime.Now).TotalSeconds)) : 0, 0);
            RefreshAllOpenedUis();
            RefreshAllOpenedAdminUis();
        }

        void ClearScheduleDrafts(bool clearScheduleState = false)
        {
            draftStartDate = "";
            draftStartTime = "";
            draftEndDate = "";
            draftEndTime = "";

            if (clearScheduleState)
            {
                hasStartTime = false;
                hasEndTime = false;
                startTime = DateTime.MinValue;
                endTime = DateTime.MinValue;
                isOpen = false;
                announcedMilestones.Clear();
            }

            SaveData();
        }

        void SetOpenStateWithMinutes(int minutes)
        {
            isOpen = true;
            hasStartTime = false;
            startTime = DateTime.MinValue;
            announcedMilestones.Clear();

            if (minutes > 0)
            {
                hasEndTime = true;
                endTime = DateTime.Now.AddMinutes(minutes);
                BroadcastMessage(string.Format(T("msg_open_until"), endTime));
                BroadcastHeadline(T("headline_betting_opened"), string.Format(T("detail_closes_at_short"), endTime));
            }
            else
            {
                hasEndTime = false;
                endTime = DateTime.MinValue;
                BroadcastMessage(T("msg_betting_open"));
                BroadcastHeadline(T("headline_betting_opened"), T("detail_no_close_time"));
            }

            SaveData();
            WriteLiveOverlay();
            WriteTriggerJson("open", "", GetTotalPoolAmount(), new List<string>(), hasEndTime ? Math.Max(0, (int)Math.Ceiling((endTime - DateTime.Now).TotalSeconds)) : 0, 0);
            RefreshAllOpenedUis();
            RefreshAllOpenedAdminUis();
        }

        void CloseCurrentBet(string message, string overlayText, string triggerType = "")
        {
            isOpen = false;
            hasStartTime = false;
            hasEndTime = false;
            startTime = DateTime.MinValue;
            endTime = DateTime.MinValue;
            announcedMilestones.Clear();
            BroadcastMessage(message);
            BroadcastHeadline("Closed", "Betting has been closed");
            SaveData();
            WriteOverlayText(overlayText);
            if (!string.IsNullOrEmpty(triggerType))
                WriteTriggerJson(triggerType, "", GetTotalPoolAmount(), new List<string>(), 0, 0);
            RefreshAllOpenedUis();
            RefreshAllOpenedAdminUis();
        }

        void ResetPools()
        {
            NormalizeOptions();
            totalPool.Clear();
            for (int i = 1; i <= optionCount; i++)
                totalPool[i] = 0;
        }

        [ConsoleCommand("betevent.closeui")]
        void CCmdCloseUi(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            CloseUI(player);
        }

        [ConsoleCommand("betevent.refreshui")]
        void CCmdRefreshUi(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            ShowUI(player);
        }



        [ConsoleCommand("betevent.admin.setstartdate")]
        void CCmdAdminSetStartDate(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!IsAdmin(player)) return;
            draftStartDate = arg.Args != null && arg.Args.Length > 0 ? string.Join(" ", arg.Args).Trim() : "";
            SaveData();
            ShowAdminUI(player);
        }

        [ConsoleCommand("betevent.admin.setstarttime")]
        void CCmdAdminSetStartTime(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!IsAdmin(player)) return;
            draftStartTime = arg.Args != null && arg.Args.Length > 0 ? string.Join(" ", arg.Args).Trim() : "";
            SaveData();
            ShowAdminUI(player);
        }

        [ConsoleCommand("betevent.admin.setenddate")]
        void CCmdAdminSetEndDate(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!IsAdmin(player)) return;
            draftEndDate = arg.Args != null && arg.Args.Length > 0 ? string.Join(" ", arg.Args).Trim() : "";
            SaveData();
            ShowAdminUI(player);
        }

        [ConsoleCommand("betevent.admin.setendtime")]
        void CCmdAdminSetEndTime(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!IsAdmin(player)) return;
            draftEndTime = arg.Args != null && arg.Args.Length > 0 ? string.Join(" ", arg.Args).Trim() : "";
            SaveData();
            ShowAdminUI(player);
        }

        [ConsoleCommand("betevent.admin.applyschedule")]
        void CCmdAdminApplySchedule(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!IsAdmin(player)) return;

            if (isOpen || GetTotalPoolAmount() > 0 || playerBets.Count > 0)
            {
                SendReply(player, T("msg_schedule_locked", player));
                ShowAdminUI(player);
                return;
            }

            string error;
            if (!TryApplySchedule(draftStartDate, draftStartTime, draftEndDate, draftEndTime, out error))
            {
                SendReply(player, error + "\n");
                ShowAdminUI(player);
                return;
            }

            BroadcastMessage(string.Format(T("msg_schedule_applied"), startTime, endTime));
            BroadcastHeadline("Schedule Applied", $"Starts {startTime:MM/dd HH:mm} / Closes {endTime:MM/dd HH:mm}");
            WriteOverlayText($"BET RESERVED\nStart {startTime:MM/dd HH:mm}\nCloses {endTime:MM/dd HH:mm}");
            WriteTriggerJson("reserved", "", GetTotalPoolAmount(), new List<string>(), 0, 0);
            RefreshAllOpenedUis();
            RefreshAllOpenedAdminUis();
        }

        [ConsoleCommand("betevent.admin.clearschedule")]
        void CCmdAdminClearSchedule(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!IsAdmin(player)) return;
            ClearScheduleDrafts(true);
            WriteIdleStreamFiles();
            ShowAdminUI(player);
            RefreshAllOpenedUis();
        }

        [ConsoleCommand("betevent.admin.closeui")]
        void CCmdAdminCloseUi(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            CloseAdminUI(player);
        }

        [ConsoleCommand("betevent.admin.refresh")]
        void CCmdAdminRefresh(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!IsAdmin(player)) return;
            ShowAdminUI(player);
        }

        [ConsoleCommand("betevent.admin.open")]
        void CCmdAdminOpen(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!IsAdmin(player)) return;

            int minutes = 0;
            var args = arg.Args;
            if (args != null && args.Length >= 1)
                int.TryParse(args[0], out minutes);

            SetOpenStateWithMinutes(minutes);
        }

        [ConsoleCommand("betevent.admin.close")]
        void CCmdAdminClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!IsAdmin(player)) return;
            CloseCurrentBet(T("msg_betting_closed"), T("overlay_closed_manual"), "close");
        }

        [ConsoleCommand("betevent.admin.refund")]
        void CCmdAdminRefund(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!IsAdmin(player)) return;

            RefundAll();
            BroadcastMessage(T("msg_refunded_all"));
            LogToFile("BetEvent", $"[REFUND] {DateTime.Now:yyyy-MM-dd HH:mm:ss} Full refund completed", this);
            SaveData();
            WriteOverlayText("BET REFUND\nAll bets refunded");
            WriteTriggerJson("refund", "", 0, new List<string>(), 0, 0);
            RefreshAllOpenedUis();
            RefreshAllOpenedAdminUis();
        }

        [ConsoleCommand("betevent.admin.reset")]
        void CCmdAdminReset(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!IsAdmin(player)) return;

            RefundAll();
            BroadcastMessage(T("msg_reset_done"));
            LogToFile("BetEvent", $"[RESET] {DateTime.Now:yyyy-MM-dd HH:mm:ss} Bet data reset", this);
            SaveData();
            WriteIdleStreamFiles();
            RefreshAllOpenedUis();
            RefreshAllOpenedAdminUis();
        }

        [ConsoleCommand("betevent.admin.result")]
        void CCmdAdminResult(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!IsAdmin(player)) return;

            var args = arg.Args;
            if (args == null || args.Length < 1) return;

            int rank;
            if (!int.TryParse(args[0], out rank)) return;
            if (rank < 1 || rank > optionCount) return;

            Payout(rank);
            RefreshAllOpenedUis();
            RefreshAllOpenedAdminUis();
        }

        [ConsoleCommand("betevent.admin.options")]
        void CCmdAdminOptions(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!IsAdmin(player)) return;

            var args = arg.Args;
            if (args == null || args.Length < 1) return;

            int count;
            if (!int.TryParse(args[0], out count) || count < 1 || count > MaxOptions)
                return;

            if (isOpen || GetTotalPoolAmount() > 0 || playerBets.Count > 0)
            {
                SendReply(player, T("msg_option_locked", player));
                ShowAdminUI(player);
                return;
            }

            optionCount = count;
            NormalizeOptions();
            ResetPools();
            SaveData();
            WriteIdleStreamFiles();
            ShowAdminUI(player);
            RefreshAllOpenedUis();
        }

        [ConsoleCommand("betevent.bet")]
        void CCmdBet(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            var args = arg.Args;
            if (args == null || args.Length < 2)
            {
                SendReply(player, T("msg_invalid_command", player));
                return;
            }

            CmdBet(player, "bet", new[] { args[0], args[1] });
        }

        [ChatCommand("bet")]
        void CmdBet(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            CheckAutoClose();

            if (args.Length == 0)
            {
                ShowUI(player);
                return;
            }

            var sub = args[0].ToLower();

            if (sub == "open")
            {
                if (!IsAdmin(player))
                {
                    SendReply(player, T("msg_admin_only", player));
                    return;
                }

                if (args.Length >= 2)
                {
                    if (!TrySetEndTime(args[1], out string error))
                    {
                        SendReply(player, error + "\n");
                        return;
                    }
                    isOpen = true;
                    hasStartTime = false;
                    startTime = DateTime.MinValue;
                    announcedMilestones.Clear();
                    BroadcastMessage(string.Format(T("msg_open_until"), endTime));
                    SaveData();
                    WriteLiveOverlay();
                    RefreshAllOpenedUis();
                    RefreshAllOpenedAdminUis();
                    return;
                }

                SetOpenStateWithMinutes(0);
                return;
            }

            if (sub == "close")
            {
                if (!IsAdmin(player))
                {
                    SendReply(player, T("msg_admin_only", player));
                    return;
                }

                CloseCurrentBet(T("msg_betting_closed"), T("overlay_closed_manual"), "close");
                return;
            }

            if (sub == "refund")
            {
                if (!IsAdmin(player))
                {
                    SendReply(player, T("msg_admin_only", player));
                    return;
                }

                RefundAll();
                BroadcastMessage(T("msg_refunded_all"));
                LogToFile("BetEvent", $"[REFUND] {DateTime.Now:yyyy-MM-dd HH:mm:ss} Full refund completed", this);
                SaveData();
                WriteOverlayText("BET REFUND\nAll bets refunded");
                WriteTriggerJson("refund", "", 0, new List<string>(), 0, 0);
                RefreshAllOpenedUis();
                RefreshAllOpenedAdminUis();
                return;
            }

            if (sub == "reset")
            {
                if (!IsAdmin(player))
                {
                    SendReply(player, T("msg_admin_only", player));
                    return;
                }

                RefundAll();
                BroadcastMessage(T("msg_reset_done"));
                LogToFile("BetEvent", $"[RESET] {DateTime.Now:yyyy-MM-dd HH:mm:ss} Bet data reset", this);
                SaveData();
                WriteIdleStreamFiles();
                RefreshAllOpenedUis();
                RefreshAllOpenedAdminUis();
                return;
            }

            if (sub == "time")
            {
                if (!hasEndTime)
                {
                    SendReply(player, isOpen ? T("msg_no_close_time", player) : T("msg_currently_closed", player));
                    return;
                }

                var remainText = GetRemainingText();
                SendReply(player, string.Format(T("msg_close_time_remaining", player), endTime, remainText));
                return;
            }

            if (sub == "result")
            {
                if (!IsAdmin(player))
                {
                    SendReply(player, T("msg_admin_only", player));
                    return;
                }

                if (args.Length < 2)
                {
                    SendReply(player, T("msg_usage_result", player));
                    return;
                }

                int winRank;
                if (!TryParseRank(args[1], out winRank))
                {
                    SendReply(player, T("msg_label_or_number", player));
                    return;
                }

                Payout(winRank);
                RefreshAllOpenedUis();
                RefreshAllOpenedAdminUis();
                return;
            }

            if (!isOpen)
            {
                SendReply(player, T("msg_currently_closed", player));
                return;
            }

            if (args.Length < 2)
            {
                SendReply(player, T("msg_usage_bet", player));
                return;
            }

            int rank;
            int amount;

            if (!TryParseRank(args[0], out rank))
            {
                SendReply(player, T("msg_label_or_number", player));
                return;
            }

            if (!int.TryParse(args[1], out amount) || amount <= 0)
            {
                SendReply(player, T("msg_bet_amount_min", player));
                return;
            }

            if (!TakeScrap(player, amount))
            {
                SendReply(player, T("msg_not_enough_scrap", player));
                return;
            }

            BetData data;
            if (!playerBets.TryGetValue(player.userID, out data))
            {
                data = new BetData();
                playerBets[player.userID] = data;
            }

            if (!data.Bets.ContainsKey(rank))
                data.Bets[rank] = 0;

            data.Bets[rank] += amount;
            totalPool[rank] += amount;

            var label = GetLabel(rank);
            SendReply(player, string.Format(T("msg_placed_bet", player), amount, label));
            LogToFile("BetEvent", $"[BET] {DateTime.Now:yyyy-MM-dd HH:mm:ss} player={player.displayName}({player.userID}) rank={label} amount={amount}", this);
            SaveData();
            WriteLiveOverlay();
            ShowUI(player);
        }

        [ChatCommand("beteventcfg")]
        void CmdBetProCfg(BasePlayer player, string command, string[] args)
        {
            if (!IsAdmin(player))
            {
                SendReply(player, T("msg_admin_only", player));
                return;
            }

            if (args == null || args.Length == 0)
            {
                SendReply(player, T("msg_usage_cfg", player));
                return;
            }

            var sub = args[0].ToLower();

            if (sub == "options")
            {
                if (args.Length < 2)
                {
                    SendReply(player, T("msg_usage_cfg_options", player));
                    return;
                }

                int count;
                if (!int.TryParse(args[1], out count) || count < 1 || count > MaxOptions)
                {
                    SendReply(player, T("msg_option_range", player));
                    return;
                }

                if (isOpen || GetTotalPoolAmount() > 0 || playerBets.Count > 0)
                {
                    SendReply(player, T("msg_option_locked", player));
                    return;
                }

                optionCount = count;
                NormalizeOptions();
                ResetPools();
                SaveData();
                WriteIdleStreamFiles();
                SendReply(player, string.Format(T("msg_option_set", player), optionCount));
                RefreshAllOpenedAdminUis();
                return;
            }

            if (sub == "labels")
            {
                if (args.Length < 2)
                {
                    SendReply(player, T("msg_usage_cfg_labels", player));
                    return;
                }

                if (isOpen || GetTotalPoolAmount() > 0 || playerBets.Count > 0)
                {
                    SendReply(player, T("msg_labels_locked", player));
                    return;
                }

                var raw = string.Join(" ", args, 1, args.Length - 1);
                var parts = raw.Split(',');
                var labels = new List<string>();
                foreach (var part in parts)
                {
                    var label = part.Trim();
                    if (!string.IsNullOrEmpty(label))
                        labels.Add(label);
                }

                if (labels.Count < 1 || labels.Count > MaxOptions)
                {
                    SendReply(player, T("msg_label_count_range", player));
                    return;
                }

                optionCount = labels.Count;
                optionLabels = labels;
                NormalizeOptions();
                ResetPools();
                SaveData();
                WriteIdleStreamFiles();
                SendReply(player, string.Format(T("msg_labels_updated", player), string.Join(", ", optionLabels.ToArray())));
                RefreshAllOpenedAdminUis();
                return;
            }

            SendReply(player, T("msg_usage_cfg", player));
        }


        [ChatCommand("beteventadmin")]
        void CmdBetAdmin(BasePlayer player, string command, string[] args)
        {
            if (!IsAdmin(player))
            {
                SendReply(player, T("msg_admin_only", player));
                return;
            }

            ShowAdminUI(player);
        }

        bool TrySetEndTime(string input, out string error)
        {
            error = null;
            DateTime parsed;
            if (!DateTime.TryParseExact(input, "HH:mm", null, DateTimeStyles.None, out parsed))
            {
                error = "Time must be in HH:mm format. Example: /bet open 21:30";
                return false;
            }

            var now = DateTime.Now;
            endTime = new DateTime(now.Year, now.Month, now.Day, parsed.Hour, parsed.Minute, 0);
            if (endTime <= now)
                endTime = endTime.AddDays(1);

            hasEndTime = true;
            return true;
        }

        bool TryParseRank(string input, out int rank)
        {
            rank = 0;
            if (string.IsNullOrEmpty(input)) return false;

            input = input.Trim();
            for (int i = 0; i < optionLabels.Count; i++)
            {
                if (string.Equals(optionLabels[i], input, StringComparison.OrdinalIgnoreCase))
                {
                    rank = i + 1;
                    return true;
                }
            }

            string upper = input.ToUpper();
            for (int i = 0; i < optionLabels.Count; i++)
            {
                if (string.Equals(optionLabels[i].ToUpper(), upper, StringComparison.Ordinal))
                {
                    rank = i + 1;
                    return true;
                }
            }

            if (int.TryParse(input, out rank))
                return rank >= 1 && rank <= optionCount;

            return false;
        }

        string GetLabel(int rank)
        {
            if (rank >= 1 && rank <= optionLabels.Count)
                return optionLabels[rank - 1];
            return "?";
        }

        void CheckAutoClose()
        {
            if (!isOpen || !hasEndTime) return;
            if (DateTime.Now >= endTime)
                CloseBetAutomatically();
        }

        void CloseBetAutomatically()
        {
            if (!isOpen) return;
            isOpen = false;
            hasEndTime = false;
            endTime = DateTime.MinValue;
            announcedMilestones.Clear();
            BroadcastMessage(T("msg_auto_closed"));
            BroadcastHeadline(T("headline_auto_close"), T("detail_auto_close"));
            LogToFile("BetEvent", $"[AUTO CLOSE] {DateTime.Now:yyyy-MM-dd HH:mm:ss} Auto Close", this);
            SaveData();
            WriteOverlayText(T("overlay_closed_auto"));
            WriteTriggerJson("close", "", GetTotalPoolAmount(), new List<string>(), 0, 0);
            RefreshAllOpenedUis();
            RefreshAllOpenedAdminUis();
        }

        void HandleCountdownAnnouncements(TimeSpan remaining)
        {
            if (!isOpen || !hasEndTime) return;
            if (remaining.TotalSeconds <= 0) return;

            int totalMinutesLeft = (int)Math.Ceiling(remaining.TotalMinutes);

            if (totalMinutesLeft > 5)
            {
                if (totalMinutesLeft <= 50 && totalMinutesLeft % 10 == 0)
                {
                    string key = "m" + totalMinutesLeft;
                    if (!announcedMilestones.Contains(key))
                    {
                        announcedMilestones.Add(key);
                        BroadcastMessage(string.Format(T("msg_minutes_left_with_time"), totalMinutesLeft, endTime));
                    }
                }
            }
            else
            {
                if (totalMinutesLeft >= 1)
                {
                    string key = "m" + totalMinutesLeft;
                    if (!announcedMilestones.Contains(key))
                    {
                        announcedMilestones.Add(key);
                        BroadcastMessage(string.Format(T("msg_minutes_left"), totalMinutesLeft));
                    }
                }
            }

            if (remaining.TotalSeconds <= 30 && !announcedMilestones.Contains("30s"))
            {
                announcedMilestones.Add("30s");
                BroadcastMessage(T("msg_30_seconds_left"));
                WriteOverlayText(T("overlay_final_30"));
                WriteTriggerJson("countdown", "", GetTotalPoolAmount(), new List<string>(), 30, 0);
            }

            if (remaining.TotalSeconds <= 10)
            {
                int sec = (int)Math.Ceiling(remaining.TotalSeconds);
                if (sec < 1) sec = 1;
                string key = "sec" + sec;
                if (!announcedMilestones.Contains(key))
                {
                    announcedMilestones.Add(key);
                    PrintToChat($"<size=20><color=#FF4040>[BetEvent] {sec} seconds remaining!</color></size>");
                    WriteOverlayText($"BET FINAL\n{sec} seconds remaining!");
                    WriteTriggerJson("countdown", "", GetTotalPoolAmount(), new List<string>(), sec, 0);
                }
            }
        }

        void RefundAll()
        {
            foreach (var entry in playerBets)
            {
                foreach (var bet in entry.Value.Bets)
                    QueueScrapOrGive(entry.Key, bet.Value);
            }

            playerBets.Clear();
            ResetPools();
            isOpen = false;
            hasStartTime = false;
            hasEndTime = false;
            startTime = DateTime.MinValue;
            endTime = DateTime.MinValue;
            announcedMilestones.Clear();
            draftStartDate = "";
            draftStartTime = "";
            draftEndDate = "";
            draftEndTime = "";
            SaveData();
            RefreshAllOpenedAdminUis();
        }

        void Payout(int winRank)
        {
            int total = GetTotalPoolAmount();
            int winTotal = totalPool.ContainsKey(winRank) ? totalPool[winRank] : 0;
            string label = GetLabel(winRank);

            if (total <= 0)
            {
                BroadcastMessage(T("msg_no_bets_result_cancelled"));
                return;
            }

            if (winTotal <= 0)
            {
                BroadcastMessage(string.Format(T("msg_no_winners"), label));
                BroadcastHeadline(T("headline_result_finalized"), string.Format(T("detail_result_no_winner"), label));
                LogToFile("BetEvent", $"[RESULT NO WINNER] {DateTime.Now:yyyy-MM-dd HH:mm:ss} rank={label} total={total}", this);
                WriteOverlayText(string.Format(T("overlay_result_no_winner"), label));
                WriteTriggerJson("result_no_winner", label, total, new List<string>(), 0, 0);
                return;
            }

            var winners = new List<string>();
            int payoutCount = 0;

            foreach (var entry in playerBets)
            {
                int betOnWin;
                if (!entry.Value.Bets.TryGetValue(winRank, out betOnWin)) continue;

                int reward = (int)Math.Floor((double)betOnWin / winTotal * total);
                if (reward <= 0) continue;

                QueueScrapOrGive(entry.Key, reward);
                payoutCount++;

                var target = BasePlayer.FindByID(entry.Key);
                string playerName = target != null ? target.displayName : entry.Key.ToString();
                winners.Add($"{playerName} +{reward}Scrap");

                if (target != null)
                    SendReply(target, string.Format(T("msg_congrats_won", target), reward, label));

                LogToFile("BetEvent", $"[PAYOUT] {DateTime.Now:yyyy-MM-dd HH:mm:ss} rank={label} user={entry.Key} bet={betOnWin} reward={reward}", this);
            }

            BroadcastHeadline(T("headline_result_winner"), string.Format(T("detail_result_winner"), label));
            PrintToChat("<color=#FF9BCF>━━━━━━━━━━━━━━━━━━━━</color>");
            PrintToChat($"<size=22><color=#FF6BDA>{string.Format(T("chat_result_winner"), label)}</color></size>");
            PrintToChat($"<size=18><color=#FFD700>{string.Format(T("chat_total_pool_winners"), total, payoutCount)}</color></size>");
            foreach (var winner in winners)
                PrintToChat($"<color=#7CFFB2>💰 {winner}</color>");
            PrintToChat("<color=#FF9BCF>━━━━━━━━━━━━━━━━━━━━</color>");

            LogToFile("BetEvent", $"[RESULT] {DateTime.Now:yyyy-MM-dd HH:mm:ss} rank={label} total={total} winners={payoutCount}", this);
            WriteResultStreamFiles(label, total, winners, payoutCount);

            playerBets.Clear();
            ResetPools();
            isOpen = false;
            hasEndTime = false;
            endTime = DateTime.MinValue;
            announcedMilestones.Clear();
            SaveData();
        }

        int GetTotalPoolAmount()
        {
            int total = 0;
            foreach (var v in totalPool.Values)
                total += v;
            return total;
        }

        int GetParticipantCount()
        {
            return playerBets.Count;
        }

        string GetPlayerBetSummary(ulong playerId)
        {
            BetData data;
            if (!playerBets.TryGetValue(playerId, out data) || data.Bets.Count == 0)
                return T("no_bets");

            var parts = new List<string>();
            for (int i = 1; i <= optionCount; i++)
            {
                int amount;
                if (data.Bets.TryGetValue(i, out amount) && amount > 0)
                    parts.Add($"{GetLabel(i)}:{amount}Scrap");
            }

            return parts.Count > 0 ? string.Join(" / ", parts.ToArray()) : T("no_bets");
        }

        List<string> GetParticipantLines(ulong viewerId)
        {
            var rows = new List<KeyValuePair<ulong, int>>();

            foreach (var entry in playerBets)
            {
                int sum = 0;
                foreach (var value in entry.Value.Bets.Values)
                    sum += value;
                rows.Add(new KeyValuePair<ulong, int>(entry.Key, sum));
            }

            rows.Sort((a, b) => b.Value.CompareTo(a.Value));

            var lines = new List<string>();
            int shown = 0;
            foreach (var row in rows)
            {
                if (shown >= 8) break;

                BetData data;
                if (!playerBets.TryGetValue(row.Key, out data))
                    continue;

                var player = BasePlayer.FindByID(row.Key);
                string name = player != null ? player.displayName : row.Key.ToString();
                if (row.Key == viewerId)
                    name = "★ " + name;

                var parts = new List<string>();
                for (int i = 1; i <= optionCount; i++)
                {
                    int amount;
                    if (data.Bets.TryGetValue(i, out amount) && amount > 0)
                        parts.Add($"{GetLabel(i)}:{amount}");
                }

                lines.Add(parts.Count > 0 ? $"{name}\n{string.Join(" / ", parts.ToArray())}" : $"{name}\n{T("no_bets")}");
                shown++;
            }

            if (rows.Count > 8)
                lines.Add(string.Format(T("and_more", null), rows.Count - 8));

            return lines;
        }

        string GetRemainingText(BasePlayer player = null)
        {
            if (!isOpen && hasStartTime && startTime != DateTime.MinValue)
            {
                var untilStart = startTime - DateTime.Now;
                if (untilStart.TotalSeconds <= 0) return T("status_open", player);
                if (untilStart.TotalHours >= 1)
                    return string.Format("{0} {1}h {2}m {3}s", T("starts_in", player), (int)untilStart.TotalHours, untilStart.Minutes, untilStart.Seconds);
                return string.Format("{0} {1}m {2}s", T("starts_in", player), untilStart.Minutes, untilStart.Seconds);
            }

            if (!hasEndTime) return T("not_set", player);

            var remain = endTime - DateTime.Now;
            if (remain.TotalSeconds <= 0) return T("ended", player);
            if (remain.TotalHours >= 1)
                return string.Format("{0}h {1}m {2}s", (int)remain.TotalHours, remain.Minutes, remain.Seconds);
            return string.Format("{0}m {1}s", remain.Minutes, remain.Seconds);
        }

        string GetCountdownColor()
        {
            if (!hasEndTime) return "0.35 0.35 0.35 1";
            var remain = endTime - DateTime.Now;
            if (remain.TotalSeconds <= 10)
                return DateTime.Now.Millisecond < 500 ? "1.00 0.20 0.20 1" : "1.00 1.00 1.00 1";
            if (remain.TotalSeconds <= 30) return "1.00 0.50 0.20 1";
            if (remain.TotalSeconds <= 60) return "0.92 0.30 0.35 1";
            if (remain.TotalSeconds <= 300) return "0.92 0.76 0.24 1";
            return "0.25 0.65 0.45 1";
        }

        void ShowUI(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;

            CheckAutoClose();
            CloseUI(player);
            openedUiPlayers.Add(player.userID);

            var container = new CuiElementContainer();
            int grandTotal = GetTotalPoolAmount();
            int participantCount = GetParticipantCount();
            string playerSummary = GetPlayerBetSummary(player.userID);
            string remainText = GetRemainingText(player);
            string remainColor = !isOpen && hasStartTime ? "0.35 0.55 0.95 1" : (isOpen ? GetCountdownColor() : "0.85 0.35 0.35 1");
            string statusText = !isOpen && hasStartTime ? T("status_waiting", player) : (isOpen ? T("status_open", player) : T("status_closed", player));
            string statusColor = !isOpen && hasStartTime ? "0.35 0.55 0.95 1" : (isOpen ? "0.25 0.65 0.45 1" : "0.85 0.35 0.35 1");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.96 0.94 1.00 0.95" },
                RectTransform = { AnchorMin = "0.05 0.04", AnchorMax = "0.95 0.96" },
                CursorEnabled = true
            }, "Overlay", UiName);

            container.Add(new CuiLabel
            {
                Text = { Text = T("player_ui_title", player), FontSize = 24, Align = TextAnchor.MiddleCenter, Color = "0.35 0.25 0.45 1" },
                RectTransform = { AnchorMin = "0.08 0.94", AnchorMax = "0.92 0.99" }
            }, UiName);

            container.Add(new CuiLabel
            {
                Text = { Text = statusText, FontSize = 16, Align = TextAnchor.MiddleCenter, Color = statusColor },
                RectTransform = { AnchorMin = "0.40 0.90", AnchorMax = "0.60 0.94" }
            }, UiName);

            container.Add(new CuiLabel
            {
                Text = { Text = hasStartTime && !isOpen ? string.Format("{0}: {1:MM/dd HH:mm}", T("start_time", player), startTime) : (hasEndTime ? string.Format("{0}: {1:MM/dd HH:mm}", T("end_time", player), endTime) : string.Format("{0}: {1}", T("end_time", player), T("not_set", player))), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.35 0.35 0.35 1" },
                RectTransform = { AnchorMin = "0.05 0.86", AnchorMax = "0.66 0.90" }
            }, UiName);

            container.Add(new CuiLabel
            {
                Text = { Text = !isOpen && hasStartTime ? remainText : string.Format("{0}: {1}", T("time_until_close", player), remainText), FontSize = 18, Align = TextAnchor.MiddleCenter, Color = remainColor },
                RectTransform = { AnchorMin = "0.05 0.81", AnchorMax = "0.66 0.86" }
            }, UiName);

            container.Add(new CuiPanel
            {
                Image = { Color = "0.92 0.96 1.00 0.95" },
                RectTransform = { AnchorMin = "0.05 0.75", AnchorMax = "0.66 0.80" }
            }, UiName, UiName + "_RefreshHint");

            container.Add(new CuiLabel
            {
                Text = { Text = T("refresh_hint", player), FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.30 0.35 0.45 1" },
                RectTransform = { AnchorMin = "0.02 0.00", AnchorMax = "0.98 1.00" }
            }, UiName + "_RefreshHint");

            container.Add(new CuiLabel
            {
                Text = { Text = string.Format("{0}: {1} Scrap", T("total_pool", player), grandTotal), FontSize = 15, Align = TextAnchor.MiddleCenter, Color = "0.35 0.35 0.35 1" },
                RectTransform = { AnchorMin = "0.05 0.71", AnchorMax = "0.32 0.75" }
            }, UiName);

            container.Add(new CuiLabel
            {
                Text = { Text = string.Format("{0}: {1}", T("participants", player), participantCount), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.35 0.35 0.35 1" },
                RectTransform = { AnchorMin = "0.32 0.71", AnchorMax = "0.66 0.75" }
            }, UiName);

            container.Add(new CuiPanel
            {
                Image = { Color = "0.98 0.96 0.82 0.95" },
                RectTransform = { AnchorMin = "0.05 0.64", AnchorMax = "0.66 0.705" }
            }, UiName, UiName + "_MyStatus");

            container.Add(new CuiLabel
            {
                Text = { Text = string.Format("{0}: {1}", T("your_bets", player), playerSummary), FontSize = 13, Align = TextAnchor.MiddleLeft, Color = "0.32 0.28 0.22 1" },
                RectTransform = { AnchorMin = "0.03 0.00", AnchorMax = "0.97 1.00" }
            }, UiName + "_MyStatus");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.90 0.95 1.00 0.92" },
                RectTransform = { AnchorMin = "0.69 0.14", AnchorMax = "0.95 0.90" }
            }, UiName, UiName + "_Players");

            container.Add(new CuiLabel
            {
                Text = { Text = T("participant_list", player), FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.25 0.25 0.35 1" },
                RectTransform = { AnchorMin = "0.00 0.94", AnchorMax = "1.00 0.99" }
            }, UiName + "_Players");

            var participantLines = GetParticipantLines(player.userID);
            float participantY = 0.85f;
            for (int i = 0; i < participantLines.Count; i++)
            {
                string bgColor = i % 2 == 0 ? "0.96 0.98 1.00 0.75" : "0.92 0.96 1.00 0.75";
                if (participantLines[i].StartsWith("★ "))
                    bgColor = "0.98 0.92 1.00 0.92";

                container.Add(new CuiPanel
                {
                    Image = { Color = bgColor },
                    RectTransform = { AnchorMin = string.Format("0.04 {0}", participantY), AnchorMax = string.Format("0.96 {0}", participantY + 0.07f) }
                }, UiName + "_Players", UiName + "_Players_Row_" + i);

                container.Add(new CuiLabel
                {
                    Text = { Text = participantLines[i], FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "0.22 0.24 0.30 1" },
                    RectTransform = { AnchorMin = "0.04 0.00", AnchorMax = "0.96 1.00" }
                }, UiName + "_Players_Row_" + i);

                participantY -= 0.082f;
                if (participantY < 0.02f)
                    break;
            }

            var sortedRanks = new List<int>();
            for (int i = 1; i <= optionCount; i++)
                sortedRanks.Add(i);
            sortedRanks.Sort((a, b) =>
            {
                int amountA = totalPool.ContainsKey(a) ? totalPool[a] : 0;
                int amountB = totalPool.ContainsKey(b) ? totalPool[b] : 0;
                int poolCompare = amountB.CompareTo(amountA);
                return poolCompare != 0 ? poolCompare : a.CompareTo(b);
            });

            int displayCount = sortedRanks.Count;
            int columns = displayCount <= 4 ? 1 : 2;
            int rows = (int)Math.Ceiling(displayCount / (float)columns);
            float cardsTop = 0.61f;
            float cardsBottom = 0.14f;
            float cardsHeight = cardsTop - cardsBottom;
            float rowGap = 0.008f;
            float cardHeight = (cardsHeight - ((rows - 1) * rowGap)) / rows;
            float cardsLeft = 0.05f;
            float cardsRight = 0.66f;
            float cardsWidth = cardsRight - cardsLeft;
            float colGap = 0.012f;
            float cardWidth = columns == 1 ? cardsWidth : (cardsWidth - colGap) / 2f;

            for (int index = 0; index < sortedRanks.Count; index++)
            {
                int rank = sortedRanks[index];
                int row = index / columns;
                int column = index % columns;

                float cardYMax = cardsTop - row * (cardHeight + rowGap);
                float cardYMin = cardYMax - cardHeight;
                float cardXMin = cardsLeft + column * (cardWidth + colGap);
                float cardXMax = cardXMin + cardWidth;

                bool playerHasBet = false;
                BetData data;
                int ownAmount = 0;
                if (playerBets.TryGetValue(player.userID, out data))
                    playerHasBet = data.Bets.TryGetValue(rank, out ownAmount) && ownAmount > 0;

                int rankPool = totalPool.ContainsKey(rank) ? totalPool[rank] : 0;
                string rowColor = "0.82 0.91 1.00 0.92";
                if (index == 0) rowColor = "0.98 0.86 0.96 0.96";
                else if (index == 1) rowColor = "0.90 0.96 1.00 0.96";
                else if (index == 2) rowColor = "0.90 0.98 0.90 0.96";
                if (playerHasBet) rowColor = "0.92 0.88 1.00 0.98";

                double odds = rankPool > 0 ? (double)grandTotal / rankPool : 0d;
                double percent = grandTotal > 0 ? (double)rankPool / grandTotal * 100d : 0d;
                string oddsText = rankPool > 0 ? string.Format("{0} {1:0.00}x / {2:0}%%", T("approx", player), odds, percent) : T("waiting_for_bets", player);
                string rankBadge = index == 0 ? T("popular_1", player) : index == 1 ? T("popular_2", player) : index == 2 ? T("popular_3", player) : "";
                string ownText = playerHasBet ? string.Format("{0}: {1} Scrap", T("you_bet", player), ownAmount) : "";
                string rowName = UiName + "_Card_" + rank;

                container.Add(new CuiPanel
                {
                    Image = { Color = rowColor },
                    RectTransform = { AnchorMin = string.Format("{0:0.000} {1:0.000}", cardXMin, cardYMin), AnchorMax = string.Format("{0:0.000} {1:0.000}", cardXMax, cardYMax) }
                }, UiName, rowName);

                container.Add(new CuiLabel
                {
                    Text = { Text = string.Format("{0} {1}", T("option_prefix", player), GetLabel(rank)), FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "0.25 0.25 0.35 1" },
                    RectTransform = { AnchorMin = "0.03 0.66", AnchorMax = "0.20 0.96" }
                }, rowName);

                container.Add(new CuiLabel
                {
                    Text = { Text = string.Format("{0}: {1} Scrap", T("current", player), rankPool), FontSize = 13, Align = TextAnchor.MiddleLeft, Color = "0.25 0.25 0.35 1" },
                    RectTransform = { AnchorMin = "0.03 0.40", AnchorMax = "0.42 0.72" }
                }, rowName);

                container.Add(new CuiLabel
                {
                    Text = { Text = oddsText, FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "0.30 0.30 0.40 1" },
                    RectTransform = { AnchorMin = "0.03 0.15", AnchorMax = "0.42 0.42" }
                }, rowName);

                if (!string.IsNullOrEmpty(rankBadge))
                {
                    container.Add(new CuiLabel
                    {
                        Text = { Text = rankBadge, FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0.42 0.28 0.48 1" },
                        RectTransform = { AnchorMin = "0.23 0.66", AnchorMax = "0.38 0.94" }
                    }, rowName);
                }

                if (!string.IsNullOrEmpty(ownText))
                {
                    container.Add(new CuiLabel
                    {
                        Text = { Text = ownText, FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0.54 0.22 0.52 1" },
                        RectTransform = { AnchorMin = "0.24 0.42", AnchorMax = "0.43 0.68" }
                    }, rowName);
                }

                string[] buttonColors = { "0.74 0.86 1.00 1.00", "0.86 0.96 0.78 1.00", "0.98 0.88 0.72 1.00", "0.96 0.82 0.90 1.00" };
                float buttonWidth = 0.12f;
                float buttonGap = 0.02f;
                float buttonStart = 0.46f;
                if (columns == 1)
                {
                    buttonWidth = 0.115f;
                    buttonGap = 0.02f;
                    buttonStart = 0.46f;
                }
                else
                {
                    buttonWidth = 0.1125f;
                    buttonGap = 0.01f;
                    buttonStart = 0.50f;
                }

                for (int bi = 0; bi < buttonAmounts.Length; bi++)
                {
                    int amount = buttonAmounts[bi];
                    float minX = buttonStart + bi * (buttonWidth + buttonGap);
                    float maxX = minX + buttonWidth;
                    if (maxX > 0.98f)
                        maxX = 0.98f;

                    container.Add(new CuiButton
                    {
                        Button = { Command = string.Format("betevent.bet {0} {1}", rank, amount), Color = buttonColors[bi] },
                        RectTransform = { AnchorMin = string.Format("{0:0.000} 0.18", minX), AnchorMax = string.Format("{0:0.000} 0.82", maxX) },
                        Text = { Text = amount.ToString(), FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.20 0.20 0.30 1" }
                    }, rowName);
                }
            }

            container.Add(new CuiButton
            {
                Button = { Command = "betevent.refreshui", Color = "0.98 0.92 0.70 1.00" },
                RectTransform = { AnchorMin = "0.12 0.05", AnchorMax = "0.28 0.10" },
                Text = { Text = T("refresh", player), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.25 0.25 0.30 1" }
            }, UiName);

            container.Add(new CuiButton
            {
                Button = { Command = "betevent.closeui", Color = "1.00 0.75 0.75 1.00" },
                RectTransform = { AnchorMin = "0.33 0.05", AnchorMax = "0.49 0.10" },
                Text = { Text = T("close", player), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.30 0.20 0.20 1" }
            }, UiName);

            if (IsAdmin(player))
            {
                container.Add(new CuiButton
                {
                    Button = { Command = "chat.say /beteventadmin", Color = "0.90 0.84 1.00 1.00" },
                    RectTransform = { AnchorMin = "0.54 0.05", AnchorMax = "0.70 0.10" },
                    Text = { Text = T("admin_ui", player), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.26 0.22 0.36 1" }
                }, UiName);
            }

            CuiHelper.AddUi(player, container);
        }

        string GetAdminStatusText(BasePlayer player)
        {
            if (!isOpen && hasStartTime && hasEndTime)
                return string.Format("{0} / {1} {2:MM/dd HH:mm} / {3} {4:MM/dd HH:mm}", T("status_waiting", player), T("start", player), startTime, T("closes", player), endTime);
            if (!isOpen && hasStartTime)
                return string.Format("{0} / {1} {2:MM/dd HH:mm}", T("status_waiting", player), T("start", player), startTime);
            if (isOpen && hasEndTime)
                return string.Format("{0} / {1} {2:MM/dd HH:mm}", T("status_open", player), T("closes", player), endTime);
            if (isOpen)
                return string.Format("{0} / {1}", T("status_open", player), T("not_set", player));
            return T("status_closed", player);
        }

        string GetOptionLabelsText()
        {
            var parts = new List<string>();
            for (int i = 1; i <= optionCount; i++)
                parts.Add($"{i}:{GetLabel(i)}");
            return string.Join(" / ", parts.ToArray());
        }

        void ShowAdminUI(BasePlayer player)
        {
            if (!IsAdmin(player) || !player.IsConnected) return;

            CheckAutoClose();
            CloseUI(player);
            CloseAdminUI(player);
            openedAdminUiPlayers.Add(player.userID);

            var container = new CuiElementContainer();
            int grandTotal = GetTotalPoolAmount();
            int participantCount = GetParticipantCount();

            container.Add(new CuiPanel
            {
                Image = { Color = "0.97 0.92 0.98 0.96" },
                RectTransform = { AnchorMin = "0.18 0.06", AnchorMax = "0.82 0.94" },
                CursorEnabled = true
            }, "Overlay", AdminUiName);

            container.Add(new CuiLabel
            {
                Text = { Text = T("admin_ui_title", player), FontSize = 24, Align = TextAnchor.MiddleCenter, Color = "0.38 0.22 0.42 1" },
                RectTransform = { AnchorMin = "0.08 0.93", AnchorMax = "0.92 0.99" }
            }, AdminUiName);

            container.Add(new CuiPanel
            {
                Image = { Color = "0.94 0.97 1.00 0.95" },
                RectTransform = { AnchorMin = "0.05 0.80", AnchorMax = "0.95 0.91" }
            }, AdminUiName, AdminUiName + "_Summary");

            container.Add(new CuiLabel
            {
                Text = { Text = string.Format("{0}: {1}\n{2}: {3} Scrap   {4}: {5}\n{6}: {7}   {8}: {9}\n{10}: {11}\n{12}: {13}\n{14}: {15}", T("admin_status", player), GetAdminStatusText(player), T("total_pool", player), grandTotal, T("participants", player), participantCount, T("options", player), optionCount, T("labels", player), GetOptionLabelsText(), T("scheduled_start", player), hasStartTime ? startTime.ToString("MM/dd HH:mm") : T("not_set", player), T("scheduled_close", player), hasEndTime ? endTime.ToString("MM/dd HH:mm") : T("not_set", player), T("remaining", player), GetRemainingText(player)), FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "0.24 0.24 0.30 1" },
                RectTransform = { AnchorMin = "0.03 0.05", AnchorMax = "0.97 0.95" }
            }, AdminUiName + "_Summary");

            container.Add(new CuiLabel
            {
                Text = { Text = T("entry_actions", player), FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "0.30 0.22 0.34 1" },
                RectTransform = { AnchorMin = "0.06 0.72", AnchorMax = "0.35 0.78" }
            }, AdminUiName);

            string[] openTexts = { T("open_now", player), "10 min", "30 min", "60 min" };
            string[] openCmds = { "betevent.admin.open 0", "betevent.admin.open 10", "betevent.admin.open 30", "betevent.admin.open 60" };
            string[] openColors = { "0.82 0.95 0.82 1", "0.82 0.92 1.00 1", "0.90 0.88 1.00 1", "0.98 0.88 0.82 1" };
            float[] openX = { 0.06f, 0.27f, 0.48f, 0.69f };

            for (int i = 0; i < openTexts.Length; i++)
            {
                container.Add(new CuiButton
                {
                    Button = { Command = openCmds[i], Color = openColors[i] },
                    RectTransform = { AnchorMin = string.Format("{0} 0.64", openX[i]), AnchorMax = string.Format("{0} 0.70", openX[i] + 0.18f) },
                    Text = { Text = openTexts[i], FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.22 0.22 0.28 1" }
                }, AdminUiName);
            }

            container.Add(new CuiButton
            {
                Button = { Command = "betevent.admin.close", Color = "1.00 0.78 0.78 1.00" },
                RectTransform = { AnchorMin = "0.06 0.56", AnchorMax = "0.28 0.62" },
                Text = { Text = T("close_entries", player), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.30 0.18 0.18 1" }
            }, AdminUiName);

            container.Add(new CuiButton
            {
                Button = { Command = "betevent.admin.refund", Color = "0.98 0.90 0.72 1.00" },
                RectTransform = { AnchorMin = "0.31 0.56", AnchorMax = "0.53 0.62" },
                Text = { Text = T("refund_all", player), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.28 0.22 0.14 1" }
            }, AdminUiName);

            container.Add(new CuiButton
            {
                Button = { Command = "betevent.admin.reset", Color = "0.88 0.86 0.96 1.00" },
                RectTransform = { AnchorMin = "0.56 0.56", AnchorMax = "0.78 0.62" },
                Text = { Text = T("reset", player), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.22 0.20 0.34 1" }
            }, AdminUiName);

            container.Add(new CuiLabel
            {
                Text = { Text = T("option_settings", player), FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "0.30 0.22 0.34 1" },
                RectTransform = { AnchorMin = "0.06 0.46", AnchorMax = "0.55 0.52" }
            }, AdminUiName);

            int optionColumns = 6;
float optionBaseX = 0.06f;
float optionBaseY = 0.39f;
float optionWidth = 0.10f;
float optionHeight = 0.06f;
float optionGapX = 0.11f;
float optionGapY = 0.07f;
for (int i = 1; i <= MaxOptions; i++)
{
    int optionRow = (i - 1) / optionColumns;
    int optionCol = (i - 1) % optionColumns;
    float optionMinX = optionBaseX + optionCol * optionGapX;
    float optionMinY = optionBaseY - optionRow * optionGapY;

    container.Add(new CuiButton
    {
        Button = { Command = $"betevent.admin.options {i}", Color = i == optionCount ? "0.94 0.78 0.96 1.00" : "0.86 0.94 1.00 1.00" },
        RectTransform = { AnchorMin = string.Format("{0:0.00} {1:0.00}", optionMinX, optionMinY), AnchorMax = string.Format("{0:0.00} {1:0.00}", optionMinX + optionWidth, optionMinY + optionHeight) },
        Text = { Text = i.ToString(), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.22 0.22 0.30 1" }
    }, AdminUiName);
}

            container.Add(new CuiLabel
            {
                Text = { Text = T("settle_result", player), FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "0.30 0.22 0.34 1" },
                RectTransform = { AnchorMin = "0.06 0.30", AnchorMax = "0.35 0.36" }
            }, AdminUiName);

            int resultColumns = 6;
float resultBaseX = 0.06f;
float resultBaseY = 0.215f;
float resultWidth = 0.12f;
float resultHeight = 0.055f;
float resultGapX = 0.13f;
float resultGapY = 0.065f;
for (int i = 1; i <= optionCount; i++)
{
    int resultRow = (i - 1) / resultColumns;
    int resultCol = (i - 1) % resultColumns;
    float resultMinX = resultBaseX + resultCol * resultGapX;
    float resultMinY = resultBaseY - resultRow * resultGapY;

    container.Add(new CuiButton
    {
        Button = { Command = $"betevent.admin.result {i}", Color = "0.86 1.00 0.88 1.00" },
        RectTransform = { AnchorMin = string.Format("{0:0.00} {1:0.00}", resultMinX, resultMinY), AnchorMax = string.Format("{0:0.00} {1:0.00}", resultMinX + resultWidth, resultMinY + resultHeight) },
        Text = { Text = GetLabel(i), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.22 0.22 0.28 1" }
    }, AdminUiName);
}

            container.Add(new CuiPanel
            {
                Image = { Color = "0.97 0.94 0.98 0.96" },
                RectTransform = { AnchorMin = "0.05 0.00", AnchorMax = "0.95 0.17" }
            }, AdminUiName, AdminUiName + "_SchedulePanel");

            // ===== Reservation Settings UI（修正版） =====
            container.Add(new CuiLabel
            {
                Text = { Text = T("schedule_settings", player), FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "0.30 0.22 0.34 1" },
                RectTransform = { AnchorMin = "0.03 0.80", AnchorMax = "0.42 0.96" }
            }, AdminUiName + "_SchedulePanel");

            container.Add(new CuiLabel
            {
                Text = { Text = T("start", player), FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.25 0.35 0.55 1" },
                RectTransform = { AnchorMin = "0.03 0.52", AnchorMax = "0.08 0.72" }
            }, AdminUiName + "_SchedulePanel");

            container.Add(new CuiLabel
            {
                Text = { Text = T("date", player), FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "0.24 0.24 0.30 1" },
                RectTransform = { AnchorMin = "0.09 0.52", AnchorMax = "0.13 0.72" }
            }, AdminUiName + "_SchedulePanel");

            container.Add(new CuiPanel
            {
                Image = { Color = "1.00 1.00 1.00 0.90" },
                RectTransform = { AnchorMin = "0.13 0.50", AnchorMax = "0.25 0.74" }
            }, AdminUiName + "_SchedulePanel", AdminUiName + "_StartDateBg");

            container.Add(new CuiElement
            {
                Parent = AdminUiName + "_StartDateBg",
                Name = AdminUiName + "_StartDateInput",
                Components =
                {
                    new CuiInputFieldComponent { Command = "betevent.admin.setstartdate ", Text = draftStartDate, FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "0.20 0.20 0.25 1", CharsLimit = 10 },
                    new CuiRectTransformComponent { AnchorMin = "0.03 0.08", AnchorMax = "0.97 0.92" }
                }
            });

            container.Add(new CuiLabel
            {
                Text = { Text = T("time", player), FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "0.24 0.24 0.30 1" },
                RectTransform = { AnchorMin = "0.26 0.52", AnchorMax = "0.30 0.72" }
            }, AdminUiName + "_SchedulePanel");

            container.Add(new CuiPanel
            {
                Image = { Color = "1.00 1.00 1.00 0.90" },
                RectTransform = { AnchorMin = "0.30 0.50", AnchorMax = "0.38 0.74" }
            }, AdminUiName + "_SchedulePanel", AdminUiName + "_StartTimeBg");

            container.Add(new CuiElement
            {
                Parent = AdminUiName + "_StartTimeBg",
                Name = AdminUiName + "_StartTimeInput",
                Components =
                {
                    new CuiInputFieldComponent { Command = "betevent.admin.setstarttime ", Text = draftStartTime, FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "0.20 0.20 0.25 1", CharsLimit = 5 },
                    new CuiRectTransformComponent { AnchorMin = "0.05 0.08", AnchorMax = "0.95 0.92" }
                }
            });

            container.Add(new CuiLabel
            {
                Text = { Text = T("end", player), FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.55 0.25 0.25 1" },
                RectTransform = { AnchorMin = "0.45 0.52", AnchorMax = "0.50 0.72" }
            }, AdminUiName + "_SchedulePanel");

            container.Add(new CuiLabel
            {
                Text = { Text = T("date", player), FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "0.24 0.24 0.30 1" },
                RectTransform = { AnchorMin = "0.51 0.52", AnchorMax = "0.55 0.72" }
            }, AdminUiName + "_SchedulePanel");

            container.Add(new CuiPanel
            {
                Image = { Color = "1.00 1.00 1.00 0.90" },
                RectTransform = { AnchorMin = "0.55 0.50", AnchorMax = "0.67 0.74" }
            }, AdminUiName + "_SchedulePanel", AdminUiName + "_EndDateBg");

            container.Add(new CuiElement
            {
                Parent = AdminUiName + "_EndDateBg",
                Name = AdminUiName + "_EndDateInput",
                Components =
                {
                    new CuiInputFieldComponent { Command = "betevent.admin.setenddate ", Text = draftEndDate, FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "0.20 0.20 0.25 1", CharsLimit = 10 },
                    new CuiRectTransformComponent { AnchorMin = "0.03 0.08", AnchorMax = "0.97 0.92" }
                }
            });

            container.Add(new CuiLabel
            {
                Text = { Text = T("time", player), FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "0.24 0.24 0.30 1" },
                RectTransform = { AnchorMin = "0.68 0.52", AnchorMax = "0.72 0.72" }
            }, AdminUiName + "_SchedulePanel");

            container.Add(new CuiPanel
            {
                Image = { Color = "1.00 1.00 1.00 0.90" },
                RectTransform = { AnchorMin = "0.72 0.50", AnchorMax = "0.80 0.74" }
            }, AdminUiName + "_SchedulePanel", AdminUiName + "_EndTimeBg");

            container.Add(new CuiElement
            {
                Parent = AdminUiName + "_EndTimeBg",
                Name = AdminUiName + "_EndTimeInput",
                Components =
                {
                    new CuiInputFieldComponent { Command = "betevent.admin.setendtime ", Text = draftEndTime, FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "0.20 0.20 0.25 1", CharsLimit = 5 },
                    new CuiRectTransformComponent { AnchorMin = "0.05 0.08", AnchorMax = "0.95 0.92" }
                }
            });

            container.Add(new CuiLabel
            {
                Text = { Text = T("schedule_note", player), FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.38 0.34 0.44 1" },
                RectTransform = { AnchorMin = "0.03 0.27", AnchorMax = "0.72 0.42" }
            }, AdminUiName + "_SchedulePanel");

            container.Add(new CuiButton
            {
                Button = { Command = "betevent.admin.refresh", Color = "0.98 0.92 0.70 1.00" },
                RectTransform = { AnchorMin = "0.16 0.03", AnchorMax = "0.30 0.20" },
                Text = { Text = T("refresh", player), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.25 0.25 0.30 1" }
            }, AdminUiName + "_SchedulePanel");

            container.Add(new CuiButton
            {
                Button = { Command = "betevent.admin.closeui", Color = "1.00 0.75 0.75 1.00" },
                RectTransform = { AnchorMin = "0.32 0.03", AnchorMax = "0.46 0.20" },
                Text = { Text = T("close", player), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.30 0.20 0.20 1" }
            }, AdminUiName + "_SchedulePanel");

            container.Add(new CuiButton
            {
                Button = { Command = "betevent.admin.applyschedule", Color = "0.84 0.96 0.84 1.00" },
                RectTransform = { AnchorMin = "0.70 0.03", AnchorMax = "0.84 0.20" },
                Text = { Text = T("apply_schedule", player), FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "0.22 0.28 0.22 1" }
            }, AdminUiName + "_SchedulePanel");

            container.Add(new CuiButton
            {
                Button = { Command = "betevent.admin.clearschedule", Color = "0.96 0.86 0.86 1.00" },
                RectTransform = { AnchorMin = "0.86 0.03", AnchorMax = "0.99 0.20" },
                Text = { Text = T("clear_schedule", player), FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "0.30 0.20 0.20 1" }
            }, AdminUiName + "_SchedulePanel");

            CuiHelper.AddUi(player, container);
        }

        void RefreshAllOpenedAdminUis()
        {
            var ids = new List<ulong>(openedAdminUiPlayers);
            foreach (var userId in ids)
            {
                var player = BasePlayer.FindByID(userId);
                if (player == null || !player.IsConnected || !player.IsAdmin)
                {
                    openedAdminUiPlayers.Remove(userId);
                    continue;
                }
                ShowAdminUI(player);
            }
        }

        void RefreshAllOpenedUis()
        {
            var ids = new List<ulong>(openedUiPlayers);
            foreach (var userId in ids)
            {
                var player = BasePlayer.FindByID(userId);
                if (player == null || !player.IsConnected)
                {
                    openedUiPlayers.Remove(userId);
                    continue;
                }
                ShowUI(player);
            }
        }

        void BroadcastHeadline(string title, string detail = "")
        {
            PrintToChat("<color=#FF9BCF>━━━━━━━━━━━━━━━━━━━━</color>");
            if (string.IsNullOrEmpty(detail))
            {
                PrintToChat($"<size=20><color=#FF6BDA>[BetEvent] {title}</color></size>");
            }
            else
            {
                PrintToChat($"<size=20><color=#FF6BDA>[BetEvent] {title}</color></size>\n<size=16><color=#FFD700>{detail}</color></size>");
            }
            PrintToChat("<color=#FF9BCF>━━━━━━━━━━━━━━━━━━━━</color>");
        }

        void BroadcastMessage(string message)
        {
            PrintToChat($"<color=#FF9BCF>[BetEvent]</color> {message}");
        }

        void CloseUI(BasePlayer player)
        {
            if (player == null) return;
            openedUiPlayers.Remove(player.userID);
            CuiHelper.DestroyUi(player, UiName);
            CuiHelper.DestroyUi(player, "BetUI");
            CuiHelper.DestroyUi(player, "BetEventUI");
        }

        void CloseAdminUI(BasePlayer player)
        {
            if (player == null) return;
            openedAdminUiPlayers.Remove(player.userID);
            CuiHelper.DestroyUi(player, AdminUiName);
        }

        bool TakeScrap(BasePlayer player, int amount)
        {
            if (player == null || amount <= 0) return false;
            if (player.inventory == null) return false;
            if (player.inventory.GetAmount(SCRAP_ID) < amount) return false;

            player.inventory.Take(null, SCRAP_ID, amount);
            player.Command("note.inv", SCRAP_ID, -amount);
            return true;
        }

        bool IsAdmin(BasePlayer player)
        {
            return player != null && player.IsAdmin;
        }

        void OnPlayerInit(BasePlayer player)
        {
            TryDeliverPendingScrap(player);
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;
            openedUiPlayers.Remove(player.userID);
            openedAdminUiPlayers.Remove(player.userID);
        }

        void Unload()
        {
            if (monitorTimer != null)
                monitorTimer.Destroy();

            SaveData();
            WriteIdleStreamFiles();

            foreach (var player in BasePlayer.activePlayerList)
            {
                CloseUI(player);
                CloseAdminUI(player);
            }
        }

        void WriteLiveOverlay()
        {
            var sb = new StringBuilder();
            sb.AppendLine(isOpen ? "BET OPEN" : "BET CLOSED");
            if (hasEndTime)
                sb.AppendLine($"Closes {endTime:MM/dd HH:mm}");
            sb.AppendLine($"Total pool {GetTotalPoolAmount()} Scrap");
            for (int i = 1; i <= optionCount; i++)
            {
                int amount = totalPool.ContainsKey(i) ? totalPool[i] : 0;
                sb.AppendLine($"{GetLabel(i)} : {amount}");
            }
            WriteOverlayText(sb.ToString().TrimEnd());
        }

        void WriteResultStreamFiles(string label, int total, List<string> winners, int payoutCount)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Option {label} wins!");
            sb.AppendLine($"Total pool {total} Scrap");
            sb.AppendLine($"Winners {payoutCount}");
            foreach (var winner in winners)
                sb.AppendLine(winner);
            WriteOverlayText(sb.ToString().TrimEnd());
            WriteTriggerJson("result", label, total, winners, 0, payoutCount);
        }

        void WriteIdleStreamFiles()
        {
            WriteOverlayText("BET EVENT\nIdle");
            WriteTriggerJson("idle", "", 0, new List<string>(), 0, 0);
        }

        void WriteOverlayText(string text)
        {
            try
            {
                EnsureStreamPaths();
                File.WriteAllText(overlayPath, text, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                PrintWarning("Failed to write overlay output: " + ex.Message);
            }
        }

        void WriteTriggerJson(string type, string winner, int totalPoolAmount, List<string> winners, int countdownSeconds, int payoutCount)
        {
            try
            {
                EnsureStreamPaths();
                var sb = new StringBuilder();
                sb.Append("{\n");
                sb.Append("  \"type\": \"").Append(EscapeJson(type)).Append("\",\n");
                sb.Append("  \"winner\": \"").Append(EscapeJson(winner)).Append("\",\n");
                sb.Append("  \"totalPool\": ").Append(totalPoolAmount).Append(",\n");
                sb.Append("  \"countdownSeconds\": ").Append(countdownSeconds).Append(",\n");
                sb.Append("  \"payoutCount\": ").Append(payoutCount).Append(",\n");
                sb.Append("  \"timestamp\": \"").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("\",\n");
                sb.Append("  \"winners\": [");
                for (int i = 0; i < winners.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append("\"").Append(EscapeJson(winners[i])).Append("\"");
                }
                sb.Append("]\n}");
                File.WriteAllText(triggerPath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                PrintWarning("Failed to write trigger output: " + ex.Message);
            }
        }

        string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        }
    }
}
