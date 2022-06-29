using System.Collections.Generic;

namespace MinecraftStateMachine
{
    public class stateV {
        public static List<string> globalStates, silentStates;
        public static string globalState;
        public string state;
        public string val;

        public override string ToString() {
            return "{"+state+"="+val+"}";
        }
    }

    public class position {
        public float x, y, z;
        public string radius;

        public override string ToString() {
            string s = "]";
            if(radius != "..1") s = ",distance=" +radius+ "]";
            return "[x=" + x + ",y=" + y +",z="+ z + s;
        }
    }

    public class requirements {
        public stateV[] states;
        public position pos;
        public string other, execute;

        /// <summary>
        /// Minecraft format
        /// </summary>
        /// <returns></returns>
        public string toMinecraftString() {
            string s = "[";
            bool prev = false;
            if(states != null) {
                s += "scores={";
                for(int j = 0; j < states.Length; j++) {
                    if(stateV.globalStates.Contains(states[j].state)) continue;
                    if(prev == true) s += ","; prev = true;
                    s += states[j].state + "=" + states[j].val;
                }
                s += "}";
            }
            if(pos != null) {
                if(prev) s += ","; prev = true;
                s += "x=" + pos.x + ",y=" + pos.y + ",z=" + pos.z + ",distance=" + pos.radius;
            }
            if(prev && other != "]") s += ",";
            s += other;

            int ind = s.Length;

            if(states != null) for(int j = 0; j < states.Length; j++) {
                if(stateV.globalStates.Contains(states[j].state)) {
                    s += " if score " + states[j].state + " " + stateV.globalState + " matches " + states[j].val;
                }
            }
            execute = execute.Replace(MainWindow.globalStateKey, stateV.globalState);

            if(execute.Contains("~") || execute.Contains("`") || execute.Contains("sort=")) s = s.Substring(0, ind) + " at @s" + (s.Length > ind ? s.Substring(ind) : "");

            return s + execute;
        }
    }

    public class reply {
        public string msg;
        public stateV state;
        public actions action;
        public string branch;
        public int branchId;
        public int branchIndex=0;
        public bool interferes;
    }

    public class dialogue {
        public string msg;
        public string color;
        public reply[] replies;
        public bool causual;
        public stateV[] condition;

        /// <summary>
        /// Creates a raw JSON to output to users during dialogue
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="color"></param>
        /// <param name="end"></param>
        /// <param name="replies">Will be assumed null if end is true</param>
        /// <returns></returns>
        private string outputString(dialogue msg, bool end, string chatTrigger) {
            string s = "[\"\",{\"text\":\"";
            if(!msg.causual) {
                s += "\\n\\n\\n\\n\\n\\n\\n\\n\\n";
                string m = "=====";
                if(MainWindow.DEBUGMODE) m += MainWindow.DEBUGmsg2;
                while(m.Length < 43) m += "=========";
                while(m.Length < 53) m += "=";
                s += m + "\",\"color\":\"gold\"},{\"text\":\"\\n\\n";
            }
            else {
                if(MainWindow.DEBUGMODE) s += "D:" + MainWindow.DEBUGmsg2;
                s += "\\n";
            }
            s += " " + msg.msg + "\",\"color\":\"" + msg.color + "\"},{\"text\":\"\\n";
            if(!msg.causual) {
                if(msg.msg.Length <= 50) s += "\\n";
                if(msg.msg.Length <= 110) s += "\\n";
            }
            if(end) {
                if(!msg.causual) s += "\\n\\n\\n\\n\"}]";
                else s += "\\n\"}]";
            }
            else if(msg.replies != null && (msg.replies.Length > 1 || msg.replies[0].msg != "")) {
                if(!msg.causual) for(int i = 3; i >= msg.replies.Length; i--) s += "\\n";
                for(int i = 0; i < msg.replies.Length; i++) {
                    if(i != 0) s += ",{\"text\":\"";
                    s += " *" + msg.replies[i].msg + "\\n\",\"clickEvent\":{\"action\":\"run_command\",\"value\":\"/trigger " + chatTrigger + " set 1" + (i + 1) + "\"}}";
                }
                s += "]";
            }
            else {
                if(!msg.causual) s += "\\n\\n";
                s += "\\n                                     [*>]\\n\",\"clickEvent\":{\"action\":\"run_command\",\"value\":\"/trigger " + chatTrigger + " set 1"+(msg.replies != null?"1":"0")+"\"}}]";
            }
            return s;
        }

        public string[] toMinecraftString(List<string> states, bool end, int j, int id, int branchNum) {

            List<string> lines = new List<string>();
            string targetter = "@a[scores={" + states[1] + "=" + id + "," + states[2] + "=" + ((branchNum*1000) + (j * 2) + 1);
            string extra = "", gextra = "execute";
            if(condition != null) for(int k = 0; k < condition.Length; k++)
                    if(stateV.globalStates.Contains(condition[k].state)) gextra += " if score "+condition[k].state+" " + states[3] + " matches " + condition[k].val;
                    else extra += "," + condition[k].state + "=" + condition[k].val;
            extra += "}]";
            if(gextra == "execute") gextra = "";
            else gextra += " run ";
            string write = gextra + "tellraw " + targetter + extra + " ";
            if(MainWindow.DEBUGMODE) {
                MainWindow.DEBUGmsg2 = "["+id+"<"+branchNum+"."+j+"]";
                string s = outputString(this, end, states[0]);
                if(branchNum == 0 && j == 0) s = s.Substring(0, 13) + MainWindow.DEBUGmsg1 + s.Substring(13);
                write += s;
            }
            else write += outputString(this, end, states[0]);
            
            lines.Add(write);

            if(replies != null) {
                for(int k = 0; k < replies.Length; k++) {
                    List<string> lines2 = new List<string>();
                    string tar = "@a[scores={" + states[1] + "=" + id + "," + states[2] + "=" + ((branchNum * 1000) + (j * 2) + 3) + "," + states[0] + "=1" + (k + 1) + "}]";

                    if(replies[k].state != null) {
                        string mode = "set", val = replies[k].state.val;
                        if(val[0] == '-') { mode = "remove"; val = val.Substring(1); } else if(val[0] == '+') { mode = "add"; val = val.Substring(1); }
                        lines2.Add("scoreboard players "+mode+" @s " + replies[k].state.state + " " + val);
                    }

                    if(replies[k].action != null)
                        lines2.AddRange(replies[k].action.toMinecraftString());
                    if(replies[k].branch != null) 
                        lines2.Add("scoreboard players set @s " + states[2] + " " + ((replies[k].branchId * 1000) + (replies[k].branchIndex * 2) + 1));
                    if(replies[k].interferes == true) 
                        lines2.Add("scoreboard players set @s " + states[0] + " 0");

                    // add reply actions
                    if(lines2.Count == 1) {
                        if(lines2[0].Contains("@s")) lines.Add(lines2[0].Replace("@s", tar));
                        else lines.Add("execute as "+tar+" at @s run "+lines2[0]);
                    }
                    else if(lines2.Count > 1) {
                        lines.Add("tag " + tar + " add tripped");
                        for(int l = 0; l < lines2.Count; l++) lines.Add(MainWindow.reformCommand(lines2[l], "@s", "@a[tag=tripped]"));
                        lines.Add("tag @a[tag=tripped] remove tripped");
                    }
                }
            }

            if(end) lines.Add("tag " + targetter + "}]" + " add tripped");
            else {
                lines.Add(gextra + "scoreboard players set " + targetter + extra + " " + states[2] + " " + ((branchNum * 1000) + (j + 1) * 2));
                if(extra.Length > 2) lines.Add("scoreboard players set " + targetter + "}] " + states[2] + " " + ((branchNum * 1000) + (j * 2) + 3));
            }

            return lines.ToArray();
        }
    }

    public class dialogueBranch
    {
        public string name;
        public dialogue[] dialogue;

    }

    public class actions
    {
        public stateV[] states;
        public string[] commands;

        public override string ToString() {
            bool prev = false;
            string s = "[";
            if(states != null) {
                s += "[";
                foreach(stateV st in states) {
                    if(prev) s += ",";
                    prev = true;
                    s += st.ToString();
                }
                s += "]";
            }
            if(commands != null) {
                if(prev) s += ",";
                prev = false;
                foreach(string st in commands) {
                    if(prev) s += ",";
                    prev = true;
                    s += st;
                }
            }
            return s;
        }
        public string[] toMinecraftString() {
            List<string> lines = new List<string>();
            if(states != null) for(int j = 0; j < states.Length; j++) {
                    string mode = "set", val = states[j].val;
                    if(val[0] == '-') {mode = "remove"; val=val.Substring(1);} else if(val[0] == '+') {mode = "add"; val = val.Substring(1);}
                    if(stateV.globalStates.Contains(states[j].state)) lines.Add("scoreboard players "+mode+" "+ states[j].state + " " + stateV.globalState + " " + val);
                    else lines.Add("scoreboard players "+mode+" @s " + states[j].state + " " + val);
                    if(MainWindow.DEBUGMODE && !stateV.silentStates.Contains(states[j].state)) lines.Add("tellraw @s [\" [DEBUG]  State " + states[j].state + " set to " + states[j].val + "\"]");
                }
            if(commands != null) for(int j = 0; j < commands.Length; j++) {
                    commands[j] = commands[j].Replace(MainWindow.globalStateKey, stateV.globalState);
                    lines.Add(commands[j]);
                }
            return lines.ToArray();
        }
    }

    public class trigger
    {
        public string name;
        public requirements req;
        public dialogue[] dialogue;
        public dialogueBranch[] branches;
        public actions before;
        public actions after;
        public bool debugMsg;
        public int id;
        public string original;

        public override string ToString() {
            string s = "";
            if(before != null) {
                s += " " + before.ToString();
            }
            if(after != null) {
                s += " " + after.ToString();
            }
            
            return name + " ("+id+") "+req.ToString() + s;
        }
    }
}
