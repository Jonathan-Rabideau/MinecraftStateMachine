using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ArgumentParsing;
using ArgumentBuilder;

namespace MinecraftStateMachine
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static string globalStateKey = "{GLOBALSTATE}";
        public static bool DEBUGMODE = false;
        public static string DEBUGmsg1;
        public static string DEBUGmsg2;

        private readonly ArgumentParser parser;
        private readonly Formatter format;
        private BuilderWindow builder;

        public string versionCode="";
        public string versionModifier = "";
        public bool resetScoreboardsInSeparateFile = false;

        public string firstComment="";
        public string statePrefix = "";
        public List<string> globalStates_;
        public List<int> silentStates_;

        public List<string> fileTemp;
        public List<string> triggerTemp;
        public List<int> triggerStart;
        public List<int> triggerEnd;

        public MainWindow() {
            InitializeComponent();
            ConsoleChat.consoleChat = new ConsoleChat {
                lstOutput = lstOutput
            };
            Console.SetOut(ConsoleChat.consoleChat);

            globalStates_ = new List<string>();
            silentStates_ = new List<int>();
            fileTemp = new List<string>();
            triggerTemp = new List<string>();
            triggerStart = new List<int>();
            triggerEnd = new List<int>();

            Func<string, string, object, object>[] type_parsers_list = new Func<string, string, object, object>[] {
                (o, s, c) => { // stateCheck and Assign
                    object[] data = parser.quickArgs(s, null, false, null, false, null);
                    return new stateV() { state = (globalStates_.Contains(data[0]) ? "" : statePrefix)+data[0], val = (string)data[1] };
                },
                 null,
                (o, s, c) => { // position
                    object[] data = parser.quickArgs(s, new string[] { null, null, null, null, null, null, "radius", "..1.25" }, true, new string[]{ "decimal", "decimal", "decimal", null }, false, null);
                    return new position() { x = (float)(double)data[0], y = (float)(double)data[1], z = (float)(double)data[2], radius=(string)data[3] };
                },
                (o, s, c) => { // dialogue branch
                    object[] data = parser.quickArgs(s, null, false, new string[]{ "string", "dialogue[]"}, false, null);
                    return new dialogueBranch() { name = (string)data[0], dialogue = (dialogue[])data[1] };
                },
                (o, s, c) => { // requirements
                    object[] data = parser.quickArgs(s, new string[] { "states", "pos", "other", "execute", "state" }, false, new string[]{ "stateCheck[]", "position", "string", "string", "stateCheck" }, false, null);
                    if(data[4] != null) data[0] = new stateV[]{ (stateV)data[4] };
                    if(data[2] == null) data[2] = "]";
                    else data[2] = data[2] + "]";
                    if(data[3] != null) data[3] = " " + data[3];
                    else data[3] = "";
                    return new requirements() { states = (stateV[])data[0], other = (string)data[2], pos = (position)data[1], execute=(string)data[3] };
                },
                (o, s, c) => { // action
                    object[] data = parser.quickArgs(s, new string[] { "states", "cmds", "state" }, false, new string[] { "stateAssign[]", "string[]", "stateAssign" }, false, null);
                    if(data[2] != null) data[0] = new stateV[] { (stateV)data[2] };
                    return new actions() { states=(stateV[])data[0], commands=(string[])data[1] };
                },
                (o, s, c) => { // reply
                    object[] data = parser.quickArgs(s, new string[] { null, null, "state", null, "action", null, "branch", null, "branchindex", "-1", "interferes", "f" }, true, new string[]{ "string", "stateAssign", "action", "string", "integer", null }, false, null);
                    return new reply() { msg=(string)data[0], state = (stateV)data[1], action = (actions)data[2], branch = (string)data[3], branchIndex = (int)data[4], interferes=data[5].ToString().ToLower()[0] == 't' };
                },
                (o, s, c) => { // dialogue
                    object[] data = parser.quickArgs(s, new string[] { null, null, "color", "white", "replies", null, "casual", "f", "hexcolor", null, "conditions", null }, true, new string[]{ "string", null, "reply[]", null, null, "stateCheck[]" }, false, null);
                    if(data[4] != null && (string)data[1] == "white") data[1] = data[4];
                    return new dialogue() { msg=(string)data[0], color=(string)data[1], replies=(reply[])data[2], causual=((string)data[3])[0]=='t', condition = (stateV[])data[5] };
                }
            };

            parser = new ArgumentParser(true);

            ArgumentParser.type string_type = ArgumentParser.type.stringPreset;
            parser.addType(string_type);
            parser.addType(new ArgumentParser.type("stringNoQuotes", s=>(s.Length>2 && s[0]=='-'), null, (o,s,c)=>s.Substring(1), null));
            parser.addLiteralList("(states)", new string[0]);
            parser.addLiteralList("(colors)", new string[] { "black", "dark_blue", "dark_green", "dark_aqua", "dark_red", "dark_purple", "gold", "gray", "dark_gray", "blue", "green", "aqua", "red", "light_purple", "yellow", "white" });

            // When two types are similar, the one that could be both should be checked first
            // and the more specific one should have the first as an equivalence, and give it parse priority if needed
            parser.addType(new ArgumentParser.type("range", s => s.Contains("..") && int.TryParse(safeSubString(s, s.IndexOf("..") + 2, -1), out int a) && int.TryParse(safeSubString(s, 0, s.IndexOf("..")), out int b),
                null, (o, s, c) => s, new string[] { "integer" }, null, true));
            parser.addType(new ArgumentParser.type("rangeD", s => s.Contains("..") && double.TryParse(safeSubString(s, s.IndexOf("..") + 2, -1), out double a) && double.TryParse(safeSubString(s, 0, s.IndexOf("..")), out double b),
                null, (o, s, c) => s, new string[] { "integer", "decimal", "range" }, null, true));
            parser.addType(new ArgumentParser.type("intAssign", s => "+-".Contains(s[0]) && int.TryParse(s.Substring(1), out int a),
                null, (o, s, c) => s, new string[] { "integer" }, null, true));

            ArgumentParser.compoundType stateCheck_type = new ArgumentParser.compoundType("stateCheck", new string[] { "name", "value" }, new string[] { "(states)", "range" }, new int[] { 0, -1 }, 2, type_parsers_list[0]);
            parser.addType(stateCheck_type);
            parser.addType(new ArgumentParser.arrayType(stateCheck_type, "state", typeof(stateV)));

            ArgumentParser.compoundType stateAssign_type = new ArgumentParser.compoundType("stateAssign", new string[] { "name", "value" }, new string[] { "(states)", "intAssign" }, new int[] { 0, -1 }, 2, type_parsers_list[0]);
            parser.addType(stateAssign_type);
            parser.addType(new ArgumentParser.arrayType(stateAssign_type, "state", typeof(stateV)));


            ArgumentParser.compoundType position_type = new ArgumentParser.compoundType("position", new string[] { "x", "y", "z", "radius" }, new string[] { "decimal", "decimal", "decimal", "rangeD" }, 3, type_parsers_list[2]);
            parser.addType(position_type);

            ArgumentParser.compoundType requirements_type = new ArgumentParser.compoundType("requirements", new string[] { "states", "pos", "other", "execute", "state" }, new string[] { "stateCheck[]", "position", "string", "string", "stateCheck" }, 0, type_parsers_list[4]);
            parser.addType(requirements_type);

            parser.addType(new ArgumentParser.arrayType("string[]", "str", "string", ArgumentParser.arrayType.arrayParser(string_type.parse, "str", typeof(string)) ));
            ArgumentParser.compoundType action_type = new ArgumentParser.compoundType("action", new string[] { "states", "cmds", "state" }, new string[] { "stateAssign[]", "string[]", "stateAssign" }, 0, type_parsers_list[5]);
            parser.addType(action_type);

            ArgumentParser.compoundType reply_type = new ArgumentParser.compoundType("reply", new string[] { "msg", "state", "action", "branch", "branchIndex", "interferes" }, new string[] { "string", "stateAssign", "action", "string", "integer", "boolean" }, 1, type_parsers_list[6]);
            parser.addType(reply_type);                                                                                                         // 0-based index!
            parser.addType(new ArgumentParser.arrayType(reply_type, "reply", typeof(reply)));

            parser.addType(new ArgumentParser.type("hexcolor", s=>(s.Length == 7 && s[0] == '#'), null, (o, s, c) => s, null));
            ArgumentParser.compoundType dialogue_type = new ArgumentParser.compoundType("dialogue", new string[] { "msg", "color", "replies", "casual", "hexcolor", "conditions" }, new string[] { "string", "(colors)", "reply[]", "boolean", "hexcolor", "stateCheck[]" }, new int[] { -1, 0, -1, -1, -1, -1 }, 1, type_parsers_list[7]);
            parser.addType(dialogue_type);
            parser.addType(new ArgumentParser.arrayType(dialogue_type, "dialogue", typeof(dialogue)));

            ArgumentParser.compoundType dialogueBranch_type = new ArgumentParser.compoundType("dialogueBranch", new string[] { "name", "dialogue" }, new string[] { "string", "dialogue[]" }, 2, type_parsers_list[3]);
            parser.addType(dialogueBranch_type);
            parser.addType(new ArgumentParser.arrayType(dialogueBranch_type, "dialogueBranch", typeof(dialogueBranch)));

            ArgumentParser.command trigger_cmd = new ArgumentParser.command("trigger", 2, new string[] { "name", "requirements", "dialogue", "branches", "before", "after", "debugmsg" }, new string[] { "stringNoQuotes", "requirements", "dialogue[]", "dialogueBranch[]", "action", "action", "boolean" });
            parser.registerCommand(trigger_cmd, null);

            foreach(object o in parser.getAllDefinitions()) lstOutput.Items.Add(o);
            // (implicit states: dialogue, dialogueNum, chat, manages dialogue)
            
            lstOutput.Items.Add("");
            lstOutput.Items.Add("settings.txt options:");
            lstOutput.Items.Add("file list:<auto populate file list>");
            lstOutput.Items.Add("version:<version code in format x.x.x>");
            lstOutput.Items.Add("versionModifier:<version modifier in format 0.x.++>");
            lstOutput.Items.Add("    modifier key: x:input, ++:increment, --:decrement, 123:constant");
            lstOutput.Items.Add("separateFileForScoreboardReset:<true/false>");

            if(File.Exists("settings.txt")) {
                StreamReader file = File.OpenText("settings.txt");
                while(!file.EndOfStream) {
                    string line = file.ReadLine();
                    if (line.ToLower().StartsWith("file list:")) txtFile.Text = line.Substring(10);
                    else if(line.ToLower().StartsWith("version:")) versionCode = line.Substring(8);
                    else if(line.ToLower().StartsWith("versionmodifier:")) versionModifier = line.Substring(16);
                    else if(line.ToLower().StartsWith("separatefileforscoreboardreset:t")) resetScoreboardsInSeparateFile=true;
                }
                file.Close();
            }

            format = new Formatter();
            format.add("c", "{name:sub,1}");
            format.add("stateCheck", "{name}={value}");
            format.add("stateCheck[]", "State Check[<num>]");
            format.add("stateAssign", "{name}={value}");
            format.add("stateAssign[]", "State Assign[<num>]");
            format.add("position", "({x},{y},{z})<if,radius,, r=>{radius}");
            format.add("requirements", "Requirements, States:<count,states><if,state,one><if,pos,, Pos, >");
            format.add("action", "Action/<name>, States:<count,states,ignorenull><if,state,1><if,cmds,, Cmds:><count,cmds,ignorenull>");
            format.add("reply", "({msg:sub:1,16})<if,state,, State><if,action,, Action><if,branch,, Branch:>{branch}<if,branch,->{branchIndex}");
            format.add("reply[]", "Reply[<count>]");
            format.add("dialogue", "{color}, ({msg:sub:1,16}), <count,replies,ignorenull><if,replies, replies, ><count,conditions,ignorenull><if,conditions, condition(s)>");
            format.add("dialogue[]", "Dialogue[<num>]");
            format.add("dialogueBranch", "{name}, <count,dialogue> dialogue");
            format.add("dialogueBranch[]", "Dialogue Branch[<num>]");
            format.add("string[]", "String/<name>[<num>]");
        }

        #region manage UI
        private void BtnHelp_Click(object sender, RoutedEventArgs e) {
            if(!parser.hasHelp()) {
                lstOutput.Items.Clear();
                string[] re = parser.helpMultipleLines(txtCommand.Text, txtCommand.SelectionStart, null);
                for(int i = 0; i < re.Length; i++) lstOutput.Items.Add(new ListBoxItem() { Content = re[i] });
            }
            string next = parser.scrollHelp(txtCommand.Text, out int s1, out int s2);
            if(s1 != -1) {
                txtCommand.Text = next;
                txtCommand.SelectionStart = s1;
                txtCommand.SelectionLength = s2 - s1;
            }
            txtCommand.Focus();
        }
        private void txtCommand_PreviewTextInput(object sender, TextCompositionEventArgs e) {
            if(e.Text == "\t") return;
            lstOutput.Items.Clear(); parser.resetHelp();
            if(!parser.hasHelp() && e.Text==" " && e.Device is KeyboardDevice && (e.Device as KeyboardDevice).Modifiers.HasFlag(ModifierKeys.Control)) try {
                e.Handled = true;
                BtnHelp_Click(null, null);
            }catch{ parser.resetHelp(); }
        }
        bool doNotRun = false;
        private void txtCommand_TextChanged(object sender, TextChangedEventArgs e) {
            if(doNotRun) return;
            if(e.Changes.First().RemovedLength > 0 && e.Changes.First().AddedLength == 0 || e.UndoAction == UndoAction.Undo || e.UndoAction == UndoAction.Redo) { lstOutput.Items.Clear(); parser.resetHelp(); }
            else if(txtCommand.Text.Contains('\t')) {
                int num = txtCommand.Text.Count(c=>c=='\t')*3 + txtCommand.SelectionStart;
                doNotRun = true;
                txtCommand.Text = txtCommand.Text.Replace("\t", "    ");
                txtCommand.SelectionStart = num;
                doNotRun = false;
            }
            if(e.Changes.Count != 1) { }
        }

        private void BtnParse_Click(object sender, RoutedEventArgs e) {
            lstOutput.Items.Clear();
            object[] re = parser.parseMultipleLines(txtCommand.Text, null);
            for(int i = 0; i < re.Length; i++) lstOutput.Items.Add(new ListBoxItem() { Content = re[i].ToString() });
        }

        private void LstOutput_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if(((ListBox)sender).SelectedItem != null) {
                string next = parser.scrollHelp(txtCommand.Text, lstOutput.SelectedIndex-1, out int s1, out int s2);
                if(s1 != -1) {
                    txtCommand.Text = next;
                    txtCommand.SelectionStart = s1;
                    txtCommand.SelectionLength = s2 - s1;
                    txtCommand.Focus();
                }
            }
        }
        private void chkMonospaced_Checked(object sender, RoutedEventArgs e) {
            if((sender as CheckBox).IsChecked == true) txtCommand.FontFamily = new FontFamily("Consolas");
            else txtCommand.FontFamily = txtFile.FontFamily;
            txtCommand.Focus();
        }
        private void chkTabHelps_Checked(object sender, RoutedEventArgs e) {
            if((sender as CheckBox).IsChecked == true) txtCommand.AcceptsTab = false;
            else txtCommand.AcceptsTab = true;
            txtCommand.Focus();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            if(builder != null && builder.IsActive) {
                builder.Close();
                if(builder.HasChanges) e.Cancel = true;
            }
        }

        #region ArgumentBuilder
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if((sender as TabControl).SelectedItem == tabBuilder) {
                cboFilePick.ItemsSource = txtFile.Text.Split(',');
                cboFilePick.SelectedIndex = 0;
            }
        }

        private bool saveCallback(string[] lines) {
            string[] names = new string[lines.Length];
            for(int j = 0; j<lines.Length; j++) names[j] = safeSubString(lines[j], 0, lines[j].IndexOf(' '));
            for(int i=triggerTemp.Count-1; i>=0; i--) {
                for(int j=lines.Length-1; j>=0; j--) {
                    if(names[j] == triggerTemp[i]) {
                        fileTemp.Insert(triggerStart[i], lines[j]);
                        names[j] = null;
                        lines[j] = null;
                        break;
                    }
                }
            }

            StreamWriter file = File.CreateText(cboFilePick.SelectedItem.ToString());
            foreach(string s in fileTemp) file.WriteLine(s);
            foreach(string s in lines) if(s != null) file.WriteLine(s);
            file.Close();

            return true;
        }

        private void Button_Click(object sender, RoutedEventArgs e_) {
            BtnGetStates_Click(null, null);
            fileTemp.Clear();
            triggerTemp.Clear();
            triggerEnd.Clear();
            triggerStart.Clear();

            List<string> triggers = new List<string>();

            int line = 0, oldLine = 0;
            string fileName = null;
            StreamReader file = null;
            StreamWriter output = File.CreateText("backup.txt");
            bool endsWithTrigger = false;

            try {
                version();
                fileName = cboFilePick.SelectedItem.ToString();

                file = File.OpenText(fileName);
                string next;
                int readingState = 0;
                if(cboFilePick.SelectedIndex != 0) readingState = 10;

                next = getNext(file, ref readingState, ref line);
                while(next != "") {
                    if(readingState == 11) {
                        output.WriteLine(next);
                        triggers.Add(next);
                        endsWithTrigger = true;
                        triggerTemp.Add(safeSubString(next, 0, next.IndexOf(' ')));
                        triggerStart.Add(line);
                    }
                    else endsWithTrigger = false;
                    oldLine = line;
                    next = getNext(file, ref readingState, ref line);
                    if(triggerStart.Count != triggerEnd.Count) triggerEnd.Add(line);
                }
                output.Close();

                for(int i=0; i<triggers.Count; i++) {
                    string s = parser.verifyCommand(triggers[i]);
                    if(s != null) throw new Exception(s);
                }

                lstOutput.Items.Add("Launching BuilderWindow for " + fileName + "...");

                line = 0;
                file.BaseStream.Position = 0;
                int at = 0;
                while(!file.EndOfStream) {
                    next = file.ReadLine();
                    line++;
                    if(line == triggerEnd[at]) at++;
                    if(at < triggerTemp.Count && line == triggerStart[at]) triggerStart[at] = fileTemp.Count;
                    if(at < triggerTemp.Count && line >= triggerStart[at]) {
                        if(next.Length == 0 || next[0] == '#' || (next.Length > 1 && next.StartsWith("//")))
                            fileTemp.Add(next);
                    }
                    else fileTemp.Add(next);
                }
                file.Close();
                if(!endsWithTrigger) fileTemp.Add("#@triggers: autogenerated");

                builder = new BuilderWindow(parser, format, false, triggers.ToArray(), saveCallback, null);

                this.Hide();
                builder.ShowDialog();
                this.Show();
                builder = null;
                lstOutput.Items.Add("BuilderWindow closed");
            }
            catch(Exception e) {
                if(file != null) file.Close();
                if(output != null) output.Close();
                string location = "Error in file "+fileName;
                if(line != 0) location += " at line "+line;
                if(!this.IsActive) this.Show();
                lstOutput.Items.Add(location);
                lstOutput.Items.Add(e.Message);
                if(e.InnerException != null && e.InnerException.Message != "") {
                    lstOutput.Items.Add(" Inner exception: ");
                    lstOutput.Items.Add(e.InnerException.Message);
                    lstOutput.Items.Add(e.InnerException.StackTrace);
                }
            }
            temp = null;
        }

        #endregion

        private class ConsoleChat : System.IO.TextWriter
        {
            public static ConsoleChat consoleChat;
            internal ListBox lstOutput;
            public override Encoding Encoding { get { return Encoding.Default; } }
            public override void WriteLine(string value) { lstOutput.Items.Add(value); }
        }
        #endregion

        private void version() {
            string[] file = null;
            if(File.Exists("settings.txt")) {
                file = File.ReadAllLines("settings.txt");
                resetScoreboardsInSeparateFile = false;
                for(int i = 0; i < file.Length; i++) {
                    if(file[i].ToLower().StartsWith("version:")) versionCode = file[i].Substring(8);
                    else if(file[i].ToLower().StartsWith("versionmodifier:")) versionModifier = file[i].Substring(16);
                    else if(file[i].ToLower().StartsWith("separatefileforscoreboardreset:t")) resetScoreboardsInSeparateFile = true;
                }
            }
            if(versionCode != "") {
                if(versionModifier == "") {
                    lstOutput.Items.Add("Version: " + versionCode);
                    return;
                }

                string form = versionModifier, v = versionCode, vo = "";
                int df = -1, dv = -1, pdf = 0, pdv = 0;
                df = df != form.Length ? form.IndexOf('.', df + 1) : -2;
                dv = dv != v.Length ? v.IndexOf('.', dv + 1) : -2;

                try {
                    while(df != -2) {
                        if(df == -1) df = form.Length;
                        if(dv == -1) dv = v.Length;

                        string av = "", af = form.Substring(pdf, df - pdf);
                        if(pdv < v.Length) av = v.Substring(pdv, dv - pdv);
                        else av = "0";

                        if(pdf != 0) vo += ".";
                        if(af == "x") vo += av;
                        else if(af == "++") vo += int.Parse(av) + 1;
                        else if(af == "--") vo += int.Parse(av) - 1;
                        else vo += af;

                        pdf = df + 1; pdv = dv + 1;
                        df = df < form.Length ? form.IndexOf('.', df + 1) : -2;
                        if(dv >= 0) dv = dv < v.Length ? v.IndexOf('.', dv + 1) : -2;
                    }
                }
                catch(Exception e) {
                    throw new Exception("Version code was in an incorrect format", e);
                }
                versionCode = vo; bool found = false;
                if(File.Exists("settings.txt")) {
                    file = File.ReadAllLines("settings.txt");
                    for(int i=0; i<file.Length; i++) {
                        if(file[i].ToLower().StartsWith("version:")) {file[i] = file[i].Substring(0, 8) + vo; found=true; }
                    }
                }
                if(found) File.WriteAllLines("settings.txt", file);
                else File.AppendAllLines("settings.txt", new string[]{ "version:" + vo });
                lstOutput.Items.Add("Version: " + vo);
            }
        }

        private void BtnGetStates_Click(object sender, RoutedEventArgs ee) {
            lstOutput.Items.Clear(); parser.resetHelp();

            List<string> states = new List<string>();
            List<string> states_type = new List<string>();

            globalStates_.Clear();

            int line = 0, fileNum=0;
            string[] files = null;

            try {
                files = txtFile.Text.Split(',');
                foreach(string fileName in files) {
                    fileNum++;

                    StreamReader file = File.OpenText(fileName);
                    int readingState = 0;

                    string next = getNext(file, ref readingState, ref line);
                    while(next != "") {
                        if(readingState == 10) {
                            string[] data = next.Split(' ');
                            states.Add(data[0]);
                            if(data.Length > 1) {
                                states_type.Add(data[1]);
                                if(data[1] == "global") globalStates_.Add(data[0]);
                            }
                            else states_type.Add("local");
                        }
                        next = getNext(file, ref readingState, ref line);
                    }
                    file.Close();
                }

                parser.updateLiteralList("(states)", states.ToArray());
                lstOutput.Items.Add("Found states:");
                foreach(string s in states) lstOutput.Items.Add(s);
            }
            catch(Exception e) {
                string location = null;
                if(files != null && fileNum != 0) {
                    location = "Error in file "+files[fileNum-1];
                    if(line != 0) location += " at line " + line;
                }
                if(location != null) lstOutput.Items.Add(location);
                lstOutput.Items.Add(e.Message);
                lstOutput.Items.Add(e.StackTrace);
                if(e.InnerException != null) lstOutput.Items.Add(e.InnerException.Message);
            }
            temp = null;
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs ee) {
            lstOutput.Items.Clear(); parser.resetHelp();

            List<string> states = new List<string>();
            List<string> states_type = new List<string>();
            List<string> loadCommands = new List<string>();
            List<string> loadFunctions = new List<string>();
            List<string> runFunctions = new List<string>();
            List<string> runFunctionsBefore = new List<string>();

            List<trigger> triggers = new List<trigger>();

            globalStates_.Clear();
            silentStates_.Clear();

            int line=0;
            int fileNum = 0;
            string[] files = null;
            string projName = "StateMachine";
            StreamReader file = null;
            StreamWriter output = File.CreateText("backup.txt");

            try {
                version();
                files = txtFile.Text.Split(',');
                foreach(string fileName in files) {
                    fileNum++;
                    line = 0;

                    file = File.OpenText(fileName);
                    int readingState = 0, oldReadingState = 0;
                    string next;

                    if(fileNum == 1) {
                        next = getNext(file, ref readingState, ref line);
                        if(readingState == 1) {
                            projName = next;
                            next = getNext(file, ref readingState, ref line);
                        }

                        if(readingState == 2) {
                            if(next.Length > 4) next = next.Substring(0, 4);
                            statePrefix = next.Replace(" ", "_").ToLower() + "_st_";
                        }
                        else if(projName.Length > 4) statePrefix = projName.Substring(0, 4) + "_st_";
                        else statePrefix = projName + "_st_";
                        if(versionCode != "") output.WriteLine(projName+", version " + versionCode);
                        else output.WriteLine(projName);
                    }
                    else readingState = 10;

                    next = getNext(file, ref readingState, ref line);
                    while(next != "") {
                        if(readingState == 5) loadCommands.Add(next);
                        else if(readingState == 6) loadFunctions.Add(next);
                        else if(readingState == 7) runFunctions.Add(next);
                        else if(readingState == 8) runFunctionsBefore.Add(next);
                        else if(readingState == 10) {
                            int ind = next.IndexOf(' ');
                            if(ind == -1) ind = next.Length;
                            states.Add(next.Substring(0, ind));
                            if(ind < next.Length-1) {
                                next = next.Substring(ind + 1);
                                if(next.EndsWith("@silent")) {
                                    silentStates_.Add(states.Count - 2);
                                    next = next.Substring(0, next.IndexOf("@silent")).TrimEnd();
                                }

                                if(next.Length > 0) {
                                    states_type.Add(next);
                                    if(next == "global") globalStates_.Add(states[states.Count-1]);
                                }
                                else states_type.Add("local");
                            }
                        }
                        else if(readingState == 11) {
                            output.WriteLine(next);
                            if(oldReadingState != 11) parser.updateLiteralList("(states)", states.ToArray());
                            object[] d = parser.parse(next, null);
                            if(d[0] is string) {
                                lstOutput.Items.Add(d[0]);
                                if(d.Length > 1 && d[1] is Exception e) throw e;
                                else throw new Exception("");
                            }
                            else {
                                d = parser.unpackParseReturn(d, new string[] { "dialogue", "branches", "before", "after", "debugmsg" }, new object[] { null, null, null, null, true });
                                triggers.Add(new trigger() { name = fileName+"/"+(string)d[1], req = (requirements)d[2], dialogue = (dialogue[])d[3], branches = (dialogueBranch[])d[4], before = (actions)d[5], after = (actions)d[6], debugMsg = (bool)d[7] });
                                if(MainWindow.DEBUGMODE) triggers[triggers.Count - 1].original = next.Replace('\\', '|').Replace('"', '^').Replace('\'', '^');
                            }
                        }
                        oldReadingState = readingState;
                        next = getNext(file, ref readingState, ref line);
                    }
                    file.Close();
                }
                output.Close();

                lstOutput.Items.Add("Found "+states.Count+" state"+(states.Count==1 ? "" : "s")+" and "
                    + triggers.Count+" trigger"+(triggers.Count==1 ? "" : "s")+" in " + fileNum + " file"+(fileNum==1?"":"s")+".");
                if(MainWindow.DEBUGMODE) lstOutput.Items.Add("Output in debug mode");
                generate(projName, loadCommands.ToArray(), loadFunctions.ToArray(), runFunctions.ToArray(), runFunctionsBefore.ToArray(), states, states_type, triggers);
            }
            catch(Exception e) {
                if(file != null) file.Close();
                if(output != null) output.Close();
                string location=null;
                if(files != null && fileNum != 0) {
                    location = "Error in file "+files[fileNum-1];
                    if(line != 0) location += " at line " + line;
                }
                if(location != null) lstOutput.Items.Add(location);
                lstOutput.Items.Add(e.Message);
                lstOutput.Items.Add(e.StackTrace);
                if(e.InnerException != null && e.InnerException.Message != "") {
                    lstOutput.Items.Add(" Inner exception: ");
                    lstOutput.Items.Add(e.InnerException.Message);
                    lstOutput.Items.Add(e.InnerException.StackTrace);
                    Exception eee = e.InnerException;
                    int ei=1;
                    while(eee.InnerException != null && eee.Message != null) {
                        eee = eee.InnerException; ei++;
                        lstOutput.Items.Add("  Inner exception"+ei+": ");
                        lstOutput.Items.Add(eee.Message);
                        lstOutput.Items.Add(eee.StackTrace);
                    }
                }
            }
            temp = null;
        }

        string temp;
        int tempLine;
        private string getNext(StreamReader file, ref int readingState, ref int line) {
            string next;
            if(temp == null || readingState != 11) next = n(file, ref readingState, ref line);
            else { next = temp; line = tempLine; temp = null; }
            int lineBefore = line;
            if(readingState == 11) {
                string tempnext = null;
                while(tempnext == null) {
                    tempnext = n(file, ref readingState, ref line);
                    if(tempnext.Length > 0 && "{[(<}])> \t".Contains(tempnext[0])) {
                        next += tempnext.TrimStart();
                        tempnext = null;
                    }
                    else temp = tempnext;
                }
                tempLine = line;
                line = lineBefore;
            }
            return next;
        }
        private string n(StreamReader file, ref int readingState, ref int line) {
            string next = "";
            while(next == "" && !file.EndOfStream) {
                line++;
                next = file.ReadLine();
                if(next.Length > 0) {
                    if(next[0] == '#' || (next.Length > 1 && next[0] == '/' && next[1] == '/')) {
                        if(line == 1 && readingState == 0) {
                            if(next.Contains("@DEBUGMODE")) DEBUGMODE = true;
                            else DEBUGMODE = false;
                            firstComment = next;
                            if(firstComment.Length != 0) {
                                if(firstComment[0] != '#' || firstComment.StartsWith("//")) firstComment = "# Version:" + versionCode + " " + firstComment.Replace("//", "");
                                else firstComment = "# Version:" + versionCode + " " + firstComment.Substring(1);
                            }
                            firstComment = firstComment.Replace("@newline", "\r\n#");
                        }
                        if(next.ToLower().Contains("@state")) readingState = 10;
                        else if(next.ToLower().Contains("@trigger")) readingState = 11;
                        else if(next.ToLower().Contains("@loadcommand")) readingState = 5;
                        else if(next.ToLower().Contains("@loadfunction")) readingState = 6;
                        else if(next.ToLower().Contains("@runfunction")) {
                            readingState = 7;
                            if(next.ToLower().Contains("before")) readingState++;
                        }
                        next = "";
                    }
                    else {
                        if(readingState == 0) readingState = 1;
                        else if(readingState == 1) readingState = 2;
                    }
                }
            }
            return next;
        }

        private void generate(string projName, string[] loadCommands, string[] loadFunctions, string[] runFunctions, string[] runFunctionsBefore, List<string> states, List<string> states_type, List<trigger> triggers) {
            projName = projName.Replace(" ", "_");
            lstOutput.Items.Add("Creating datapack...   ");

            // base structure
            string mainDirectory = projName + @"\data\" + projName.ToLower() + @"\functions\";
            Directory.CreateDirectory(projName + @"\data\minecraft\tags\functions");
            Directory.CreateDirectory(mainDirectory+"dialogue");
            foreach(string fileName in Directory.EnumerateFiles(mainDirectory + "dialogue\\")) {
                File.Delete(fileName);
            }

            int totalFiles = 6;
            List<int> dialogueFilesCreated = new List<int>();

            // standard files in every datapack
            StreamWriter file = File.CreateText(projName + @"\pack.mcmeta");
            file.Write("{\"pack\":{\"pack_format\":1,\"description\":\""+projName+"\"}}");
            file.Close();

            file = File.CreateText(projName + @"\data\minecraft\tags\functions\load.json");
            file.Write("{\"values\":[\""+projName.ToLower()+":load\"]}");
            file.Close();

            file = File.CreateText(projName + @"\data\minecraft\tags\functions\tick.json");
            file.Write("{\"values\":[\"" + projName.ToLower() + ":run\"]}");
            file.Close();

            // include managed states
            projName = projName.ToLower();
            List<string> globalStates = new List<string>();
            List<string> timerStates = new List<string>();
            for(int i = states_type.Count - 1; i >= 0; i--)
                if(states_type[i] == "local") states_type[i] = "dummy";
                else if(states_type[i] == "timer") { states_type[i] = "dummy"; timerStates.Add(states[i]); }
                else if(states_type[i] == "global") { globalStates.Add(states[i]); states.RemoveAt(i); states_type.RemoveAt(i); }
            for(int i = 0; i < states.Count; i++) states[i] = statePrefix + states[i];
            for(int i = 0; i < timerStates.Count; i++) timerStates[i] = statePrefix + timerStates[i];
            stateV.silentStates = new List<string>();
            for(int i = 0; i<silentStates_.Count; i++) stateV.silentStates.Add(states[silentStates_[i]]);
            states.Insert(0, statePrefix.Substring(0, 5) + "chat");
            states.Insert(1, statePrefix.Substring(0, 5) + "dialogue");
            states.Insert(2, statePrefix.Substring(0, 5) + "dialogueNum");
            states.Insert(3, statePrefix.Substring(0, 5) + "global");
            states_type.Insert(0, "trigger");
            states_type.Insert(1, "dummy");
            states_type.Insert(2, "dummy");
            states_type.Insert(3, "dummy");
            stateV.globalState = states[3];
            stateV.globalStates = globalStates;


            file = File.CreateText(mainDirectory + "run.mcfunction");
            file.WriteLine(firstComment+"\r\n");
            if(MainWindow.DEBUGMODE) {
                file.WriteLine("#check if everything compiled...");
                file.WriteLine("execute if entity @a[tag=verifyCompiles] run scoreboard players add verifyCompiles "+states[3]+" 1");
                file.WriteLine("");
            }
            foreach(string fun in runFunctionsBefore) {
                file.WriteLine("function " + projName + ":" + fun);
            }
            file.WriteLine("");
            file.WriteLine("#Main events file");
            file.WriteLine("function "+projName+":main");
            file.WriteLine("");
            foreach(string fun in runFunctions) {
                file.WriteLine("function "+projName+":"+fun);
            }
            file.Close();

            
            // triggers file
            file = File.CreateText(mainDirectory + "main.mcfunction");
            file.WriteLine(firstComment + "\r\n");
            if(MainWindow.DEBUGMODE) {
                file.WriteLine("#check if everything compiled...");
                file.WriteLine("execute if entity @a[tag=verifyCompiles] run scoreboard players add verifyCompiles " + states[3] + " 1");
                file.WriteLine("");
            }
            file.WriteLine("#General");
            file.WriteLine("tag @a[tag=tripped] remove tripped");
            file.WriteLine("scoreboard players add @a[scores={"+states[0]+"=10..}] "+states[2]+" 1");
            for(int i=0; i<timerStates.Count; i++)
                file.WriteLine("scoreboard players remove @a[scores={" + timerStates[i] + "=1..}] " + timerStates[i] + " 1");
            file.WriteLine();
            
            for(int i=0; i<triggers.Count; i++) {
                // set trigger ID
                triggers[i].id = i + 1;

                if(MainWindow.DEBUGMODE && triggers[i].debugMsg) {
                    DEBUGmsg1 = " [DEBUG]  Triggered " + triggers[i].original + "\\n";
                }
                else DEBUGmsg1 = "";
                
                file.WriteLine("#Trigger "+triggers[i].name);
                string write = "execute as @a";
                
                if(triggers[i].req != null) write += triggers[i].req.toMinecraftString();
                
                file.WriteLine(write+ " run tag @s add tripped");
                file.WriteLine("#Before "+ triggers[i].name);

                if(triggers[i].dialogue == null && DEBUGMODE && triggers[i].debugMsg) file.WriteLine("tellraw @a[tag=tripped] [\""+DEBUGmsg1+"\"]");
                if(triggers[i].before != null) {
                    string[] lines = triggers[i].before.toMinecraftString();
                    for(int l = 0; l < lines.Length; l++) file.WriteLine(reformCommand(lines[l], "@s", "@a[tag=tripped]"));
                    if(triggers[i].dialogue == null) file.WriteLine("tag @a[tag=tripped] remove tripped");
                }

                if(triggers[i].dialogue != null) {
                    List<string> diaLines = new List<string>();

                    string[] finals;
                    if(triggers[i].branches != null) {
                        finals = new string[triggers[i].branches.Length + 1];
                    }
                    else finals = new string[1];
                    for(int j = 0; j < triggers[i].dialogue.Length; j++) {
                        // identify branches
                        if(triggers[i].dialogue[j].replies != null) {
                            for(int k = 0; k < triggers[i].dialogue[j].replies.Length; k++) {
                                if(triggers[i].dialogue[j].replies[k].branch != null) {
                                    triggers[i].dialogue[j].replies[k].branchId = -1;
                                    if(triggers[i].branches != null)
                                        for(int l=0; l< triggers[i].branches.Length; l++)
                                            if(triggers[i].branches[l].name == triggers[i].dialogue[j].replies[k].branch)
                                                triggers[i].dialogue[j].replies[k].branchId = l;
                                    triggers[i].dialogue[j].replies[k].branchId++;
                                    if(triggers[i].dialogue[j].replies[k].branchIndex == -1)
                                        triggers[i].dialogue[j].replies[k].branchIndex=0;
                                }
                                else if(triggers[i].dialogue[j].replies[k].branchIndex != -1) {
                                    triggers[i].dialogue[j].replies[k].branch = "main";
                                    triggers[i].dialogue[j].replies[k].branchId = 0;
                                }
                            }
                        }
                        string[] lines = triggers[i].dialogue[j].toMinecraftString(states, j == triggers[i].dialogue.Length - 1, j, triggers[i].id, 0);
                        for(int k = 0; k < lines.Length; k++)
                            if(j != triggers[i].dialogue.Length-1 || k != lines.Length - 1) diaLines.Add(lines[k]);
                            else finals[0] = lines[k];
                    }

                    if(triggers[i].branches != null)
                        for(int l=0; l<triggers[i].branches.Length; l++) {
                            diaLines.Add("#Branch " + triggers[i].branches[l].name);
                            for(int j = 0; j < triggers[i].branches[l].dialogue.Length; j++) {
                                if(triggers[i].branches[l].dialogue[j].replies != null) 
                                    for(int p = 0; p< triggers[i].branches[l].dialogue[j].replies.Length; p++)
                                        if(triggers[i].branches[l].dialogue[j].replies[p].branch != null) {
                                            triggers[i].branches[l].dialogue[j].replies[p].branchId = -1;
                                            if(triggers[i].branches != null)
                                                for(int o=0; o < triggers[i].branches.Length; o++)
                                                    if(triggers[i].branches[o].name == triggers[i].branches[l].dialogue[j].replies[p].branch)
                                                        triggers[i].branches[l].dialogue[j].replies[p].branchId = o;
                                            triggers[i].branches[l].dialogue[j].replies[p].branchId++;
                                            if(triggers[i].branches[l].dialogue[j].replies[p].branchIndex == -1)
                                                triggers[i].branches[l].dialogue[j].replies[p].branchIndex = 0;
                                        }
                                        else if(triggers[i].branches[l].dialogue[j].replies[p].branchIndex != -1) {
                                            triggers[i].branches[l].dialogue[j].replies[p].branch = triggers[i].branches[l].name;
                                            triggers[i].branches[l].dialogue[j].replies[p].branchId = l;
                                        }
                                string[] lines = triggers[i].branches[l].dialogue[j].toMinecraftString(states, j == triggers[i].branches[l].dialogue.Length - 1, j, triggers[i].id, l+1);
                                for(int k = 0; k < lines.Length; k++)
                                    if(j != triggers[i].branches[l].dialogue.Length - 1 || k != lines.Length - 1) diaLines.Add(lines[k]);
                                    else finals[l+1] = lines[k];
                            }
                        }
                    if(diaLines.Count != 1 || triggers[i].after != null) {
                        file.WriteLine("scoreboard players set @a[tag=tripped] " + states[1] + " " + triggers[i].id);
                        file.WriteLine("scoreboard players set @a[tag=tripped] " + states[2] + " 1");
                        file.WriteLine("tag @a[tag=tripped] remove tripped");
                        for(int j = 0; j < finals.Length; j++) diaLines.Add(finals[j]);

                        if(diaLines.Count > 2) {
                            totalFiles++; dialogueFilesCreated.Add(triggers[i].id);
                            StreamWriter dia = File.CreateText(mainDirectory + @"dialogue\d"+triggers[i].id + ".mcfunction");
                            dia.WriteLine(firstComment + "\r\n");
                            if(MainWindow.DEBUGMODE) {
                                dia.WriteLine("#check if everything compiled...");
                                dia.WriteLine("execute if entity @a[tag=verifyCompiles] run scoreboard players add verifyCompiles " + states[3] + " 1");
                                dia.WriteLine("");
                            }
                            dia.WriteLine("#Dialogue " + triggers[i].id + " (" + triggers[i].name + ")");
                            foreach(string str in diaLines) dia.WriteLine(str);
                            dia.Close();
                            file.WriteLine("execute as @a[scores={"+states[1]+"="+triggers[i].id+"},limit=1] run function "+projName + ":dialogue/d"+triggers[i].id);
                        }
                        else {
                            file.WriteLine("#Dialogue " + triggers[i].id + " (" + triggers[i].name + ")");
                            foreach(string str in diaLines) file.WriteLine(str);
                        }

                        file.WriteLine("#After "+triggers[i].name);
                        file.WriteLine("scoreboard players set @a[tag=tripped] " + states[1] + " 0");
                        file.WriteLine("scoreboard players set @a[tag=tripped] " + states[2] + " 0");
                    }
                    else {
                        string[] l = ArgumentParser.split(diaLines[0], ' ', 0, 0, true);
                        file.WriteLine(l[0] + " @a[tag=tripped] " + l[2]);
                    }

                    if(triggers[i].after != null) {
                        string[] lines = triggers[i].after.toMinecraftString();
                        for(int l = 0; l < lines.Length; l++) file.WriteLine(reformCommand(lines[l], "@s", "@a[tag=tripped]"));
                    }
                    file.WriteLine("tag @a[tag=tripped] remove tripped");
                }
                
                file.WriteLine();
            }

            file.WriteLine("#General closing");
            file.WriteLine("scoreboard players set @a[scores={" + states[0] + "=10..}] " + states[0] + " 1");
            file.WriteLine("scoreboard players enable @a " + states[0]);

            file.Close();

            // reset objectives
            if(resetScoreboardsInSeparateFile) file = File.CreateText(mainDirectory + "resetstates.mcfunction");
            else file = File.CreateText(mainDirectory + "load.mcfunction");
            file.WriteLine(firstComment + "\r\n");
            file.WriteLine("#Reset objectives");
            for(int i = 0; i < states.Count; i++) file.WriteLine("scoreboard objectives remove " + states[i]);
            for(int i = 0; i < states.Count; i++) file.WriteLine("scoreboard objectives add " + states[i] + " " + states_type[i]);
            for(int i = 0; i < states.Count; i++) file.WriteLine("scoreboard players set @a " + states[i] + " 0");
            if(resetScoreboardsInSeparateFile) {
                file.Close();
                file = File.CreateText(mainDirectory + "load.mcfunction");
                file.WriteLine(firstComment + "\r\n");
            }

            // load file
            if(loadCommands != null && loadCommands.Length > 0) {
                file.WriteLine();
                file.WriteLine("#Reset commands");
                for(int i = 0; i < loadCommands.Length; i++) file.WriteLine(loadCommands[i]);
            }
            if(loadFunctions != null && loadFunctions.Length > 0) {
                file.WriteLine();
                file.WriteLine("#Reset functions");
                for(int i = 0; i < loadFunctions.Length; i++) file.WriteLine("function "+projName+":"+loadFunctions[i]);
            }
            file.WriteLine();
            file.WriteLine("scoreboard players set @a " + states[1] + " 0");
            file.WriteLine("scoreboard players set @a " + states[2] + " 0");
            if(MainWindow.DEBUGMODE) {
                file.WriteLine("tag @p add verifyCompiles");
                file.WriteLine("scoreboard players set verifyCompiles " + states[3] + " 1");
                for(int i=0; i< dialogueFilesCreated.Count; i++) {
                    file.WriteLine("function "+projName+ ":dialogue/d"+dialogueFilesCreated[i]);
                }
                file.WriteLine("function " + projName + ":run");
                file.WriteLine("tag @p remove verifyCompiles");
            }
            file.Write("tellraw @a [\"\",{\"text\":\"\\n\\n\\n\\n\\n\\n\\n\\n\\n=====================================================\",\"color\":\"gold\"},{\"text\":\"\\n\\n [" + projName + "]  I have been reloaded\\n    Version "+versionCode+"\\n\\n");
            if(MainWindow.DEBUGMODE) {
                file.Write("Verify functions " + projName + ":   load   run   main\\n");
                string[] dialogues = new string[dialogueFilesCreated.Count];
                for(int i = 0; i < dialogueFilesCreated.Count; i++) dialogues[i] = "d"+dialogueFilesCreated[i]+"   ";
                for(int i = 0; i<dialogues.Length; i++) for(int j = i+1; j<dialogues.Length; j++) if(string.Compare(dialogues[i], dialogues[j]) > 0) { string t = dialogues[i]; dialogues[i] = dialogues[j]; dialogues[j] = t; }
                for(int i = 0; i < dialogues.Length; i++) file.Write(dialogues[i]);
                if(loadFunctions.Length != 0 || runFunctions.Length != 0) file.Write("\\nSupplemental:   "); else file.Write("\\n");
                for(int i = 0; i < loadFunctions.Length; i++) file.Write(loadFunctions[i]+"   ");
                for(int i = 0; i < runFunctions.Length; i++) file.Write(runFunctions[i]+"   ");
                file.Write("Auto verified: \",\"color\":\"blue\"},{\"score\":{\"name\":\"verifyCompiles\",\"objective\":\""+states[3]+"\"}},{\"text\":\"/" + (totalFiles-3)+" files.");
            }
            else file.Write("\\n\\n\\n");
            file.WriteLine("\\n\",\"color\":\"blue\"}]");
            file.Close();

            
            lstOutput.Items.Add("Done!");
            lstOutput.Items.Add("Created " + totalFiles + " files.");

            // actions use same type of state (should be assign_state or something)
        }

        internal static string reformCommand(string str, string find, string replace) {
            if(str.Contains("~") || str.Contains("^") || str.Contains("sort=")) return "execute as " + replace + " at @s run " + str;
            else if(str.Contains("@s")) return str.Replace(find, replace);
            else return "execute as " + replace + " run " + str;
        }

        public static string safeSubString(string str, int s, int l) {
            if(s < 0 || s >= str.Length || s + l > str.Length || l == 0) return "0";
            else if(l == -1) return str.Substring(s);
            else return str.Substring(s, l);
        }
    }
}
