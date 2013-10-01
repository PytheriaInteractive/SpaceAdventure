/*
	This script is included in the hope that it may be useful to existing UDE customers
	No support is given with regards to this script so use at your own discression.
	The purpose of this script is to convert scripts made for the original UDE into the
	UDE Advanced format.
	
	To use this script, create a new scene and drop this onto the camera or onto a new
	empty game object and hit run. You can then select a folder that contains dialogue
	and obtain a list of .txt files which you can then attempt to convert.
	Alternatively, you may manually enter a single filename to convert, if you so choose.
	
	If you have modified your version of the UDE to include custom functionality,
	you might have to modify this script but for the most part it should handle such
	customisations just fine
*/

using UnityEngine;
using System.IO;
using System.Collections;

public class UDEConvert : MonoBehaviour
{
	//contains the parsed details read from one dialogue turn
	public class crDialogueLine 
	{
		public int id			 = -1;		// each dialogue turn is assigned a number
		public int who			 = -1;		// index to actor speaking this turn
		public bool isChoice	 = false;	// is this plain text or a choice that needs making
		public ArrayList requirements = new ArrayList();	// keys required for this text to display
		public ArrayList next 		  = new ArrayList();	// which turn to display after this one
		public ArrayList keys 		  = new ArrayList();	// keys to modify if this turn is displayed
		public ArrayList line 		  = new ArrayList();	// the actual dialogue to display this turn
	}

	//requirements class
	public class crDialogueReq
	{
		public string kind;			// what kind of key is this: + or - (must have? or must not have?)
		public string name;			// key name to test against
		public int value;			// value key must meet
		public int redir;			// turn to redirect to if test fails
	}

	//basic key class
	public class crDialogueKey
	{
		public string kind;			// key type (- or + or other)
		public string name;			// the name of the key
		public string value;		// and it's value
	}

	//the actor class contains info about the actors participating in the dialogue
	public class crActor : System.Object
	{
		public string player;		// the name of the actor
		public string avatar;	// the image to use during his/her dialogue turns

		public crActor(string name, string image)
		{
			player = name;
			avatar = image;
		}		
	}

		public static bool FileIsLoaded;					// test if there was a problem opening the specified filename

		public static int lastIdCreated 	= -1;	// when automatically incrementing id numbers during file read
		public static int lastActorToSpeak 	= -1;	// when splitting large dialogue turns

		public static ArrayList lines;				// all imported dialogue lines, broken into it's parsed base elements
		public static ArrayList actors;				// list of all actors participating in conversation

		public static crDialogueLine currentLine = null;	// the line being spoken, complete with access to all RAW data
		
		public UDEConvert()
		{
			if (lines == null)	lines = new ArrayList();
			if (actors == null) actors = new ArrayList();
		}
		
		//used during the parsing of a file
		//add this dialogue turn to the array of turns
		static public void addLine(int id, bool multiChoice = false)
		{
			//if another turn was processed before this one indicate which actor spoke last. If this is the first
			//turn processed, initialize the actor vars. The purpose of this is to enable dialogue developers
			//to skip the [who] tag at every entry. If this turn is spoken by the same actor as the line before, wether as
			//part of the previous dialogue or not, the [who] tag is optional
			if (lines.Count > 0)
			{
				lastActorToSpeak	= currentLine.who;
				lastIdCreated		= currentLine.id;
			} else
			{
				//the first line must be initialized with an actor. if none is selected, instead of giving an error,
				//hard code the first line of dialogue to the first actor.
				lastActorToSpeak = 0;
				lastIdCreated = -1;
			}
			
			//error avoidance. Since each turn has to have a unique id, make sure this is the case...
			if (id == lastIdCreated) id = lastIdCreated + 1;

			//now create the dialogue turn object and give it the processed values
			currentLine = new crDialogueLine();
			currentLine.who = lastActorToSpeak;
			currentLine.id = id;
			currentLine.isChoice = multiChoice;
			//NEXT points to where dialogue should brach to. since options branch to various points in the dialogue
			//NEXT needs to be an array. If this turn is plain text then the NEXT array will always have a length of 1
			currentLine.next.Add(id + 1);

			lines.Add(currentLine);
		}

		public void ConvertToNewFormat(string fn, string newFileName, int index = 0, bool batch = false)
		{
			TextAsset FileResource;
			string[] linesArray;
			string firstField;

			int lastActorToSpeak  = 0;		// All dialogue spoken by same person unless otherwise specified
			int inputMode = 0;				// Text/ option/ other etc etc etc...
   			string[] txt;					// helper var. 
		    		    		
			FileIsLoaded = false;
		    		    		
			//load the entire file into memory
			FileResource = (TextAsset)Resources.Load(fn, typeof(TextAsset));
			if (!FileResource)
			{
				Debug.Log("Could not load file: " + fn);
				return;
			}
			
			actors.Clear();
			lines.Clear();

			//now split the file into separate lines for parsing
			linesArray = FileResource.text.Split("\n"[0]);

   			// Read lines from the file until the end of the file is reached.
			foreach (string ln in linesArray)
      			{
      				// in case the user likes to indent his files or leave trailing blanks. A tab character at the end
      				// would indicate a new field and if it is empty it could confuse/ crash the engine. So compensate...
      				// also, empty lines and lines starting with // are ignored
      				string line = ln.Trim();
	      			if ((line == "") || (line.IndexOf("//") == 0) )
	   				{
      					continue;
	      			}
			      		
	      			//in case the user wants the dialogue numbers to increment automatically
	      			//they need only write "[LINE]" in stead of "[LINE] 3" so ensure parsing format is intact
	      			if (line == "[line]" || line == "[choice]" )
	   					line += "\t" + (lastIdCreated + 1);
			   		
					// ensure tag formatting - [TAG][TAB} ...  except the [actors].
					if ( line[0] == "["[0] && line.IndexOf("]\t") == -1 && line.ToLower().IndexOf("[actors]") == -1 )
					{
						Debug.Log("Syntax Error on line #" + lastIdCreated.ToString() + "\n" + line);
						continue;
					}

	   				//next, split the line into TAG and VALUE
	   				string[] splitLine = line.Split("\t"[0]);
					if (splitLine.Length > 1)
						splitLine[0] = splitLine[0].ToLower();				

					// for readability sake only. Use firstField instead of splitLine[0]
	      			firstField = splitLine[0].Trim();

	      			if (firstField == "[actors]")
	      			{
	      				inputMode = 0;
	   				} else
	   				if (firstField == "[line]")
      				{
						inputMode = 1;
						// create a new line, set it as NOT an option list and make it point to the next ID to be created
						addLine(int.Parse(splitLine[1]), false);
      				} else
       				if (firstField == "[choice]")
	      			{
						inputMode = 2;
						// create a new line, set as option
						addLine(int.Parse(splitLine[1]), true);
	      			} else
	      			if (firstField == "[who]")
	   				{
	   					// a line must exist before a [WHO] can be assigned
	   					// all dialogue will be spoken by the last character to speak until a new actor is selected
						if (currentLine!= null)
						{
							if (splitLine[1] == "")
								 currentLine.who = lastActorToSpeak;
							else currentLine.who = int.Parse(splitLine[1]);

							lastActorToSpeak = currentLine.who;
						}				
	      			} else
	      			if (firstField == "[next]")
	   				{
	   					// a line must exist before a [NEXT] can be assigned
						if (currentLine!=null)
						{
							//when a new line is created, NEXT is automatically set to point to the next ID so
							//when an explicit [NEXT] is found, simply overwrite the auto generated value
							currentLine.next[0] = int.Parse(splitLine[1]);
						}
	      			} else
	      			if (firstField == "[require]")
	   				{
	   					// a line must exist before a [REQUIRE] can be assigned
						if (currentLine != null)
						{
							for(int x = 1; x < splitLine.Length; x++)
							{
								txt = splitLine[x].Split(" "[0]);
							    if ( (txt.Length < 4) || (txt.Length % 4 != 0))
								{
									Debug.Log("Syntax Error for [REQUIRE] on line #" + lastIdCreated.ToString() + "\n" + splitLine[x]);
								} else
								{
									crDialogueReq tmpReq = new crDialogueReq();
									tmpReq.kind  = txt[0];				// mustHave or mustNotHave
									tmpReq.name  = txt[1];				// of What
									tmpReq.value = int.Parse(txt[2]);	// how much?
									tmpReq.redir = int.Parse(txt[3]);	// where to redirect to if test fails
									currentLine.requirements.Add(tmpReq);
								}
							}							 
						}
	   				} else
	   				if (firstField == "[keys]")
	      			{
	   					// a line must exist before a [KEYS] can be assigned
						if (currentLine!=null)
						{
							for(int x = 1; x < splitLine.Length; x++)
							{
								txt = splitLine[x].Split(" "[0]);
							    if ( txt.Length < 3 || (txt.Length % 3 != 0) )
								{
									Debug.Log("Syntax Error for [KEYS] on line #" + lastIdCreated.ToString() + "\n" + splitLine[x]);
								} else
								{
									crDialogueKey tmpKey = new crDialogueKey();
									tmpKey.kind  = txt[0];		// gameKey or gameAction?
									tmpKey.name  = txt[1];		// name of key or action to take?
									tmpKey.value = txt[2];		// key value or optional action option
									currentLine.keys.Add(tmpKey);
								}
							}
						}
	      			} else
	      			{
	      				// if a line of text is read without a TAG, assume it is one of the lines of text to display
						switch (inputMode)
							{
								// mode 0 is where actors are defined for participation in the dialogue
								case 0:
								//splitLine[0] was converted to lower case. Get capitals again...
									string[] capText = line.Split("\t"[0]);
									crActor newActor = new crActor(capText[0].Trim(), splitLine[1]);
									actors.Add(newActor);
									break;

								// mode 1 is for normal text lines
								case 1:
									if (currentLine!=null)
									{
										currentLine.line.Add( line );
									}
									break;

								// mode 2 is when dealing with OPTIONS lines
								case 2:
									if (currentLine!=null)
									{
										// split the line into REDIRECT_TAG , TEXT_TO_SHOW
										currentLine.line.Add( splitLine[1] );
										
										// since I automatically created a NEXT field when I created the line, there is
										// already 1 value here and needs to be replaced. after adding the text to the
										// list, and before adding the redirect, if the number of lines and the number of
										// redirects match, it means that this is the first line I am adding so instead of
										// adding the current REDIRECT_TAG, instead overwrite the current one.
										// This test will obviously only return true for the first line of text in an OPTION
										if (currentLine.line.Count == currentLine.next.Count) {
											 currentLine.next[0] = int.Parse(splitLine[0]);
										} else {
											currentLine.next.Add(int.Parse(splitLine[0]));
										}
									}
									break;
							}
	      			}
				}
				
			 if (lines.Count == 0)
			 {
				Debug.LogWarning("No dialogue was defined for this dialogue...");
       			return;
			 }

			 if (actors.Count == 0)
			 {
				Debug.LogWarning("No actors were defined for participation in this dialogue...");
       			return;
			 }
			 ConvertToCML(newFileName);
			 if (batch)
			 	foundFiles[index] = "[done] - " + fn;
			 else
				outputText += " complete\n";
		} //open file
				
		public void ConvertToCML( string newFileName ) {
			cmlText = "<actors>\n";
			foreach (crActor a in actors) {
				cmlText += "\t<actor>name="+a.player+"\n";
			}

			cmlText += "\n<dialogue>";
			foreach (crDialogueLine a in lines) {
				cmlText += "\n\t<turn>id="+a.id;
				cmlText += "\n\t\t[who]"+a.who;
				cmlText += "\n\t\t[choice]"+ a.isChoice;
				
				//concatenate requirements
				string tempReq = string.Empty;
				foreach(crDialogueReq r in a.requirements) {
					if (tempReq != string.Empty)
						tempReq += ",";
					tempReq += r.kind + " " + r.name + " " + r.value + " " + r.redir;
				}
				if (tempReq != string.Empty)
					cmlText += "\n\t\t[require]" + tempReq;

				//concatenate keys
				string tempKeys = string.Empty;
				foreach(crDialogueKey k in a.keys) {
					if (tempKeys != string.Empty)
						tempKeys += ",";
					tempKeys += k.kind + " " + k.name + " " + k.value;
				}
				if (tempKeys != string.Empty)
					cmlText += "\n\t\t[keys]" + tempKeys;

				//concatenate choices (if required)
				string tempChoices = string.Empty;
				if (a.isChoice) {
					foreach(int c in a.next) {
						tempChoices += (tempChoices != string.Empty)
									? "," + c
									: c.ToString();
					}

				} else tempChoices = a.next[0].ToString();
				cmlText += "\n\t\t[next]" + tempChoices;
				
				foreach( string l in a.line)
				cmlText += "\n\t\t"+l;
			}
			
			StreamWriter f = new StreamWriter(File.Open(newFileName, FileMode.Create));
			if (null == f)
				return;
			
			f.WriteLine(cmlText);
			f.Close();

			return;
		}
			

    public Vector2 scrollPosition = Vector2.zero;
    string filename = "Dialogue3";
    string sourcePath;
    string savePath;
    string cmlText;
    string outputText = "";
    string batchOutput = "";
    string[] foundFiles = null;
    bool isConverted = false;
	bool[] toggles = null;
	void Start() {
 		sourcePath	= Application.dataPath + "/Resources/";
 		savePath	= Application.dataPath + "/DialogueSystem/Resources/";
 	}

    void OnGUI() {
    	GUI.Label(new Rect(05,25,100,25), "SourcePath");
    	GUI.Label(new Rect(05,80,100,25), "SavePath");
    	GUI.Label(new Rect(05,135,100,25), "Filename");
    	GUI.Label(new Rect(05,235,200,25), "Filename");
    	sourcePath = GUI.TextField(new Rect(5,50,200,30), sourcePath);
    	savePath = GUI.TextField(new Rect(5,105,200,30), savePath);
    	filename = GUI.TextField(new Rect(5,160,200,30), filename);
    	bool listFiles = GUI.Button(new Rect(110, 205, 95, 20), "List files" );
    	bool updateBatchOut = listFiles;
    	
    	if (GUI.Button(new Rect(5, 205, 95, 20), "Convert" ) ) {
    		outputText = "Processing: " + filename;
    		ConvertToNewFormat(filename , savePath + filename + ".txt");
    		if (null != foundFiles && foundFiles.Length > 0)
	    		listFiles = true;
    	} 

    	if (listFiles)	{
    		if (updateBatchOut) {
	    		batchOutput = savePath;
    		}
    		foundFiles = Directory.GetFiles(sourcePath, "*.txt");
    		toggles = new bool[foundFiles.Length];
    		for (int i = 0; i < foundFiles.Length; i++) {
    			foundFiles[i] = Path.GetFileNameWithoutExtension(foundFiles[i]);
    			if (File.Exists(batchOutput + foundFiles[i] + ".txt"))
    				foundFiles[i] = "[done] - " + foundFiles[i];
    			toggles[i] = false;
    		}
    		for (int i = 0; i < foundFiles.Length; i++) {
    		}
    		isConverted = false;
    	}
    	
    	if (!isConverted && null != foundFiles && foundFiles.Length > 0) 
    	if (GUI.Button(new Rect(245,25,200,20), "Batch convert")) {
    		int i = -1;
    		foreach(string s in foundFiles) {
    			i++;
    			if (s.IndexOf("[done]") == -1 && toggles[i])
		    		ConvertToNewFormat(s , batchOutput + Path.GetFileNameWithoutExtension(s) + ".txt", i, true);
    		}
    		outputText = "-- Batch conversion complete --";
    	} 
    	
    	if (null != foundFiles) {
        scrollPosition = GUI.BeginScrollView(new Rect(245f, 50f, 216, Screen.height - 60), scrollPosition,
        									 new Rect(0, 0, 200, foundFiles.Length * 25));
        	for (int i = 0; i < foundFiles.Length; i++) {
        		if ( foundFiles[i].IndexOf("[done]") >= 0)
		        	GUI.Label(new Rect(0, 25 * i, 200, 24), foundFiles[i]);
		        else
		        	toggles[i] = GUI.Toggle(new Rect(0, 25 * i, 200, 24), toggles[i], foundFiles[i]);
        	}
        GUI.EndScrollView();}
    }
    
} // UDEConvert