using System;

namespace GameAi
{
    // dialogue.json: 브리핑·디브리핑 대화(7.19)
    [Serializable]
    public class DialogueConfig
    {
        public int version = 1;
        public DialogueSpeaker[] speakers =
        {
            new DialogueSpeaker { id = "accomplice", name = "조력자", portrait = "accomplice" },
            new DialogueSpeaker { id = "player", name = "나", portrait = "player" },
        };
        public DialogueDef[] dialogues = new DialogueDef[0];

        public DialogueDef Find(string id)
        {
            foreach (DialogueDef d in dialogues)
                if (d != null && d.id == id) return d;
            return null;
        }

        public DialogueSpeaker Speaker(string id)
        {
            foreach (DialogueSpeaker s in speakers)
                if (s.id == id) return s;
            return new DialogueSpeaker { id = id, name = id, portrait = id };
        }
    }

    [Serializable]
    public class DialogueSpeaker
    {
        public string id = "";
        public string name = "";
        public string portrait = "";
    }

    [Serializable]
    public class DialogueDef
    {
        public string id = "";
        public DialogueLine[] lines = new DialogueLine[0];
    }

    [Serializable]
    public class DialogueLine
    {
        public string speaker = "accomplice";
        public string text = "";
        public string[] choices = { "[...]" };
    }

    // qwen.json: 로컬 Qwen 서버(MASTER 9.12)
    [Serializable]
    public class QwenConfig
    {
        public int version = 1;
        public bool enabled = true;
        public string endpoint = "http://127.0.0.1:11434/v1/chat/completions";
        public string model = "qwen2.5:7b";
        public bool warmup_on_start = true;
        public float timeout_s = 2f;
        public int max_concurrent = 1;
        public int queue_max = 4;
        public int max_tokens = 32;
        public float temperature = 0.7f;
        public int fail_stop_count = 5;
        public float merge_window_s = 0.5f;
        public float screen_center_m = 6f;
        public bool use_json_schema = true;
        public int history_per_npc = 5;
        public int history_global = 10;
        public int log_max = 200;
        public float chatter_interval_s = 60f;
        public float reply_delay_s = 1f;
        public float reply_radius_m = 10f;

        // 127.0.0.1·localhost만 허용(15장)
        public bool IsLocal
        {
            get
            {
                if (string.IsNullOrEmpty(endpoint)) return false;
                try
                {
                    var uri = new Uri(endpoint);
                    return uri.Host == "127.0.0.1" || uri.Host == "localhost";
                }
                catch (UriFormatException)
                {
                    return false;
                }
            }
        }
    }
}
