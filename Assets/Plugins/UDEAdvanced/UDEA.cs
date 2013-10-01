using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public enum eUDEAState : int
{
	Inactive			= 0,
	Active				
}

public class UDEA : MonoBehaviour
{
		
	CML event_data;
	public delegate void UDEAEventHandler(object source, UDEAEvent e);

	//the various events that fire throughout a dialogue
	static public event UDEAEventHandler OnDialogueStartFailed;	//invalid or no filename supplied
	static public event UDEAEventHandler OnDialogueStarted;		//called after first successful Speak()
	static public event UDEAEventHandler OnSpeakStart;				//when starting a new turn
	static public event UDEAEventHandler OnSpeakEnd;				//on successfully completing a turn without ending the dialogue
	static public event UDEAEventHandler OnDialogueEnded;			//when speaking turn -1
	static public event UDEAEventHandler OnTypewriterModeUpdate;	//if enabled, each time a new character is added

	public const float				UDEA_version	= 2.2f;
	
	static public eUDEAState		State;
	static public CMLGameKeys 		GameKeys;						//All GameKeys
	static public string			Language = "Afrikaans";
	
	public Texture2D				_currentAvatar { get; set; }

	// These variables can either be set in the inspector or set through scripts
	public GameObject 				parent;							// object to receive messages from dialogue
	public string 					FileName;						// file that contains the dialogue
	
	//display text gradually, one letter at a time... or not?
	public bool 					TypewriterMode	= true;
	public float 					TypewriterSpeed = 0.015f;		// speed at which the typewriter text is built up

	public string	 				FormattedText;					// if current line is text only, this will contain linesToShow as 1 string
	protected string[]				linesToShow;					// from currentLine, starting at startAtIncrement, up to maxLinesToShow
			
	const int	 					maxLinesToShow 	= 4;			// only show this many lines of text at a time even if there are more this turn
		
	//private variables are used by the UDEA only and the user has no reason to interact with them...
	cmlData			 				currentLine		= null;			// the line being spoken, complete with access to all RAW data

	int 							startAtIncrement= -1;			// if continuing a dialogue turn, skip the previous text
	bool 							mustProcessReq 	= false;		// has this turn's requirements been processed yet?

	List<cmlData>					lines;							// all imported dialogue lines, broken into it's parsed base elements
	List<cmlData>					actors;							// list of all actors participating in the conversation

	protected int 					currentlySpeakingLine	= -1;	// numeric index to what is being spoken
	protected int					selection 			 	= -1;	// when selecting options, this is the currently selected one

	bool				 			m_fileIsLoaded;					// test if there was a problem opening the specified filename
	string 							m_typewriterText;				// text to build towards during typewriter mode
		
	//initialize the class
	//since nothing can be done until a file is loaded,
	//all that needs to be done is for the static array to be created
	//according to the specified value for maxLinesToShow		
	public UDEA ()
	{
		if (GameKeys == null)
			GameKeys = new CMLGameKeys();
		State = eUDEAState.Inactive;
		linesToShow = new string[maxLinesToShow];
		lines = new List<cmlData> ();
		actors = new List<cmlData> ();
		event_data = new CML();
		event_data.AddNode("UDEAEvent", "UDEA Version="+UDEA_version);
		event_data.AddNode("EventDetails");
	}
	
	GameObject Parent { set {	parent = value;} }
	
	public bool FileIsLoaded 		{ get { return m_fileIsLoaded; } }
	public bool HasEnded 			{ get { return ((currentlySpeakingLine == -1) || (!FileIsLoaded) ); } }
	public bool CurrentIsChoice 	{ get { return (null != currentLine) ? currentLine.Bool("choice") : false;} }

	public int Selection 			{ get { return selection;} }
	public int LineCount			{ get { return lines.Count;} }
	public int ActorCount			{ get { return actors.Count;} }
	public int WhoIsSpeakingID 		{ get { return currentLine.Int("who");} }
	public int CurrentOptionCount	{ get { return currentLine.data.Count;} }

	public string[] UnformattedText { get { return linesToShow; } }	
	public string WhoIsSpeakingName { get { return actors[ WhoIsSpeakingID ]["name"]; } }
	public string CurrentText 		{ get { return string.Join("\n", linesToShow); } }

	public cmlData CurrentLine		{ get { return currentLine;} }
	public List<cmlData> Lines		{ get { return lines;} }

	public int IDOfNextLine	{
		get {
			if (!CurrentIsChoice)
				return currentLine.Int("next");
				
			string[] nexts = currentLine.String("next").Split(',');
			if (nexts.Length <= Selection)
				return 0;
			return int.Parse(nexts[Selection].Trim());	
		} }
			
	//get the avatar image for the actor speaking this turn	
	public Texture2D WhoIsSpeakingAvatar {
		get {
				Texture2D currentAvatar = null;
				string avatarName	= (actors.Count > WhoIsSpeakingID)
									? actors [ WhoIsSpeakingID ].String("avatar").Trim()
									: string.Empty;
				
				//if requesting an invalid texture, return an empty image, rather than a null
				if (avatarName == string.Empty) {
					_currentAvatar = new Texture2D(1,1);
					_currentAvatar.name = avatarName;
					return _currentAvatar;
				}
				
				//if requesting the current texture, just return it
				if (null != _currentAvatar && avatarName == _currentAvatar.name)
					return _currentAvatar;
					
				//at this point try to load the requested avatar
				currentAvatar = Resources.Load(avatarName , typeof(Texture2D)) as Texture2D;
				_currentAvatar = currentAvatar;
				
				//in case the user is requesting an avatar that doesn't exist, instead of trying to
				//load it each frame, generate an empty texture so it thinks the texture is loaded
				if (null == _currentAvatar) {
					print("test this");
					_currentAvatar= new Texture2D(1,1);
					_currentAvatar.name = avatarName;
				}

				return _currentAvatar;
			}
	}

	//hard code the dialogue to point to a turn with id -1
	protected void EndDialogue ()	{	
		currentlySpeakingLine = -1;
		m_typewriterText = string.Empty;
		m_fileIsLoaded = false;
		State = eUDEAState.Inactive ;
		destroy ();
		
		event_data[1].defined.Clear();
		_OnDialogueEnded(new UDEAEvent(event_data), this);
	}

	//get the specified dialogue turn or return the first line if the turn was not found.
	//with the dialogue's redirection system this will have the effect of ending the conversation
	//and restarting again. Although this will mean the last conversation was abruptly ended,
	//the redirection system will make it look like the actor has started a new line of conversation
	//instead of getting an error or the dialogue simply stopping
	cmlData Find(int id) {
		if (null != currentLine)
			if (currentLine.ID == id)
				return currentLine;
				
		foreach (cmlData tmp in lines)
			if (tmp.ID == id) return tmp;

		return lines[0];
	}

	//make your choice from the dialogue options. Don't change if an invalid choice is made
	public void OptionSelect(int val) {
		selection = (currentLine.data.Count <= val) ? selection : val;
	}

	//when selecting a choice from multiple options, select the previous one
	public void OptionPrevious() {
		if (selection > 0) selection--;
	}

	//when selecting a choice from multiple options, select the next one
	public void OptionNext() {
		if (selection < (currentLine.data.Count - 1))
			selection++;
	}


	public string GetConcatenatedText(string separator) {
		return string.Join(separator, linesToShow);	
	}

	//return the text associated with the specified
	public string CurrentOptions(int x) {
		return (x < maxLinesToShow) ? linesToShow[x] : linesToShow[0];
	}

	//destroy the parsed data from the last read file to free up the memory and prepare for the next
	protected void destroy() {
		lines.Clear();
		actors.Clear();	
	}

	// MAIN public void 1 ///////////////////////////////////////
	// OPEN FILE /////////////////////////////////////////////
	// call open file to load the file that contains the dialogue
	// the file will be parsed and stored as objects in an ordered array.
	// Edit: Openfile has been made a lowlevel function as of version 1.4
	// It's use is replaced with StartDialogue
		
	public void StartDialogue ( int atIndex = -3) {
		StartDialogue(FileName, atIndex);
	}
		
	public void StartDialogue (string fn = "", int atIndex = -3)
	{
		if (fn != string.Empty)
			FileName = fn;
			
		if (FileName == string.Empty) {
			State = eUDEAState.Inactive ;
			event_data[1].defined.Clear();
			event_data[1].Set("message","No filename specified");
			_OnDialogueStartFailed(new UDEAEvent(event_data), this);
			return;
		}
		if (!parent)
			parent = gameObject;

		if ( OpenFile (atIndex) ) {
			State = eUDEAState.Active ;
			_OnDialogueStarted(new UDEAEvent(event_data), this);
		} else {
			_OnDialogueStartFailed(new UDEAEvent(event_data), this);
			Debug.LogError ( event_data[1].String("message") );
		}
	}
		
	bool OpenFile()
	{
		return OpenFile(FileName);
	}

	bool OpenFile(int index)
	{
		return OpenFile(FileName, index);
	}

	bool OpenFile(string filename)
	{
		return OpenFile(filename, -3);
	}

	bool OpenFile(string fn, int startAt) {
		try {
			string lastActorToSpeak	= "0";
			m_fileIsLoaded 			= false;

			CML sections			= new CML(fn);
			actors					= sections.AllDataOfType("actor");
			lines					= sections.AllDataOfType("turn");
				
			for (int i = 0; i < lines.Count; i++) {
				if (!lines[i].defined.ContainsKey("who"))
					lines[i].Set("who", lastActorToSpeak);
	
				if (!lines[i].defined.ContainsKey("next")) {
					if (i < lines.Count - 1)
						lines[i].Set("next", (lines[i+1].ID).ToString() );
					else
						lines[i].Set("next", lines[0].ID.ToString() );
				}
				if (!lines[i].defined.ContainsKey("choice"))
					lines[i].Set("choice", "false");

				lastActorToSpeak = lines[i]["who"];
			}

			event_data[1].defined.Clear();
			if (lines.Count == 0)	throw new NoDialogueDefinedException();
			if (actors.Count == 0)	throw new NoActorsDefinedException();
			 
			//set so Speak's event handlers will have a valid response
			m_fileIsLoaded = true;
			
			//set again in response to Speak
			m_fileIsLoaded = (Speak (startAt) > -1);

			event_data[1].defined.Clear();
			if (!FileIsLoaded)
				event_data[1].Set("message", "Unable to start dialogue");
		}

		catch (NoDialogueDefinedException) {
			event_data[1].Set("message", "No dialogue was defined for this dialogue");
		}

		catch(NoActorsDefinedException){
			event_data[1].Set("message", "No actors were defined for participation in this dialogue");
		}

		return FileIsLoaded;
	}

	// MAIN public void 2 ////////////////////////////////////
	// SPEAK /////////////////////////////////////////////////
	// Speak does all the grunt work. It selects the dialogue turns to display,
	// it tests for redirection, automatically redirects if need be, modifies game keys
	// if required and sends messages to the parent object if instructed to do so by
	// the dialogue file. Once the file is loaded, programmers need only call SPEAK
	// until the dialogue has ended and it will do the rest.
	// It selects subsets of dialogue text from large turns and continues on to the next
	// turn when required.
	
	string[] LocalizedText(cmlData turn) {
		string[] results;
		if (Language == string.Empty || turn.String(Language) == string.Empty) {
			results = new string[turn.data.Count];
			turn.data.CopyTo(results);
		} else	{	
			results = turn.String(Language).Replace("\\n",'\n' +string.Empty).Split ('\n');
		}
		return results;
	}
	
	//require this version for BroadcastMessage to work
	public int Speak ( int id = -3) {
		return Speak(id, true);
	}

	public int Speak (int id, bool triggerEvent)
	{
		if (triggerEvent) {
			event_data[1].defined.Clear();
			_OnSpeakStart(new UDEAEvent(event_data), this);
		}
		cmlData tempLine;

		int continueFrom = 0;
		string[] this_turn;
		
		try {		
			//if we are instructed to quit the conversation
			if (id == -1) {
				EndDialogue ();
				return -1;
			}

			//if no line number is provided, try and determine the line
			if (id == -3) {
				//first see if we were reading something the previous turn...
				if (currentlySpeakingLine > -1) {
					id = currentlySpeakingLine;
				} else {
					//if not, then assign to first ID
					tempLine = lines [0];
					id = tempLine.ID;
				}
				
				//if we are currently speaking the same line as before, continue to show this id
				if (id == currentlySpeakingLine) {
					this_turn = LocalizedText(currentLine);
					startAtIncrement++;
					if (this_turn.Length > (startAtIncrement * maxLinesToShow)) {
						continueFrom = id;
						mustProcessReq = false;
					} else {
						continueFrom = IDOfNextLine;							
						startAtIncrement = 0;
						mustProcessReq = true;
					}
				} else {
					//if not, first determine wether we were speaking at all
					// and if we were, check what comes next...
					mustProcessReq = true;
					startAtIncrement = 0;
					continueFrom = (currentlySpeakingLine == -1) ? lines[0].ID : IDOfNextLine;
				} 
			} else {	// if ID was given to us, we HAVE to use it
				// if it is the same as the last ID
				if (id == currentlySpeakingLine) {		//if there is nothing more to read here... This is an error... Quit!!!
					this_turn = LocalizedText(currentLine);
					if (this_turn.Length > (startAtIncrement * maxLinesToShow)) {
						startAtIncrement++;
						mustProcessReq = false;
					} else {
						Debug.LogError ("User provided line number with no dialogue to show: " + id);
						EndDialogue ();
						return -1;
					}
				} else {
					//if it is a new ID
					startAtIncrement = 0;
					mustProcessReq = true;
				}
				continueFrom = id;
			}
			
			//see if we are being redirected to an end of conversation
			if (continueFrom == -1) {
				EndDialogue ();
				return -1;
			}

			//now that we know what we want, let's go get it...
			if (continueFrom != currentlySpeakingLine) {
				selection = 0;
				
				currentLine = Find (continueFrom);
				currentlySpeakingLine = currentLine.ID;
				startAtIncrement = 0;
			}

			//first process the requirements for this id
			if (GameKeys != null && mustProcessReq && ( currentLine.defined.ContainsKey("require") )) {	
				string[] requirements = TextReplace(currentLine["require"]).Split(',');
				for (int x= 0; x < requirements.Length; x++) {
					string[] tempReq = requirements[x].Trim().Split(' ');
					if (tempReq.Length != 4)
						throw new InvalidReqDefinitionException(currentLine.ID +":"+ requirements[x]);
				
					switch (tempReq[0]) {
					case "+":
						if (GameKeys.DoesNotHave ( tempReq[1], int.Parse(tempReq[2]) ) ) {
							Speak ( int.Parse( tempReq[3] ), false );
							return 0;
						}
						break;
				
					case "-":
						if (GameKeys.DoesHave ( tempReq[1], int.Parse(tempReq[2]) )) {
							Speak ( int.Parse( tempReq[3] ), false );
							return 0;
						}
						break;

					case "=":	
						if (!GameKeys.HasExactly ( tempReq[1], int.Parse(tempReq[2]) )) {
							Speak ( int.Parse( tempReq[3] ), false );
							return 0;
						}
						break;
					}
				}
			}

			//now modify the keys	
			if (GameKeys != null && mustProcessReq && (currentLine.defined.ContainsKey("keys"))) {
				string[] keys = TextReplace(currentLine["keys"]).Split(',');
				for (int x= 0; x < keys.Length; x++) {
					string[] tempKey = keys[x].Trim().Split(' ');
					if (tempKey.Length != 3) 
						throw new InvalidKeyDefinitionException(currentLine.ID +":"+ keys[x]);
					
					switch ( tempKey[0] ) {
					case "+":
						GameKeys.Add ( tempKey[1], float.Parse (tempKey[2]) );
						break;
					case "-":
						GameKeys.Subtract ( tempKey[1], float.Parse (tempKey[2]) );
						break;
					case "=":
						GameKeys.Setf ( tempKey[1], float.Parse (tempKey[2]) );
						break;
					default: 	
						parent.BroadcastMessage ("ProcessKeys", keys[x].Trim(), SendMessageOptions.DontRequireReceiver);
						break;
					}
				}
			}

			continueFrom = maxLinesToShow * startAtIncrement;

			this_turn = LocalizedText(currentLine);
			for (int x=0; x < maxLinesToShow; x++) {
				linesToShow [x] = (continueFrom < this_turn.Length) ? TextReplace ((string)this_turn[ continueFrom ] ) : "";
				continueFrom++;
			}

			if ( currentLine.Bool("choice") ) {
				FormattedText = string.Empty;
			} else {
				if (TypewriterMode)
					GradualDisplay ();
				else
					FormattedText = CurrentText;
			}
			//prepare the avatar image
			if (null == WhoIsSpeakingAvatar)
				Debug.LogWarning("No avatar was set. This should never happen!");
			
			event_data[1].defined.Clear();
			event_data[1].Set("turn_id", CurrentLine.ID.ToString());
			event_data[1].Set("text", CurrentText);
			event_data[1].Set("choice", CurrentLine.String("choice"));
			_OnSpeakEnd(new UDEAEvent(event_data), this);
			return 1;
		}
		
		catch (InvalidReqDefinitionException ex) {
			Debug.Log("Detected an incorrectly formatted Requirement field on line: " + ex.Message);
		}
		
		catch (InvalidKeyDefinitionException ex) {
			Debug.Log("Detected an incorrectly formatted Key field on line: " + ex.Message);
		}
		
		catch (Exception ex) {
			Debug.Log (ex.Message + ".\n" + ex.StackTrace);
		}
			
		return 0;
	}
			
	public string TextReplace (string s)
	{
		if (s == string.Empty || null == s)
			return string.Empty;
			
		string original = s;
		int startInt = 0;
		int endInt = 0;
		string data_type = string.Empty;
		string keyName = "value";
		string keyValue = string.Empty;

		while (original.IndexOf("{:") >= 0) {
			if (original.IndexOf ("}") == -1)
				original = original + "}";
			
			startInt = original.IndexOf ("{:");
			endInt = original.IndexOf ("}", startInt);
			data_type = original.Substring (startInt + 2, endInt - startInt - 2);
			if (data_type.IndexOf(':') > 0) {
				string[] split_key = data_type.Split(':');
				data_type = split_key[0];
				keyName = split_key[1];
			} else keyName = "value";
			
			keyValue = (GameKeys.DoesHave (data_type, 0, keyName)) ? GameKeys.Value(data_type, keyName) : "[" + data_type + "]";
			original = string.Concat (original.Substring (0, startInt),
						string.Concat (keyValue,
						original.Substring (endInt + 1, original.Length - endInt - 1)));
		}

		return original;
	}
		
	void AddLetter ()
	{
		if (m_typewriterText == FormattedText || FormattedText.Length >= m_typewriterText.Length)
			return;
			
		FormattedText += m_typewriterText [FormattedText.Length];
		event_data[1].defined.Clear();
		event_data[1].Set("text", FormattedText);
		_OnTypewriterModeUpdate(new UDEAEvent(event_data), this);
		Invoke ("AddLetter", TypewriterSpeed);
	}
		
	void GradualDisplay ()
	{
		m_typewriterText = FormattedText = string.Empty;
		if (IsInvoking ("AddLetter"))
			CancelInvoke ("AddLetter");

		m_typewriterText = CurrentText;
		FormattedText = string.Empty;

		if (!CurrentIsChoice)
			AddLetter ();
	}
	
	
	void Update ()
	{
		HandleInput ();
	}
		
	public virtual void HandleInput ()
	{
		if (HasEnded || State == eUDEAState.Inactive)
			return;
	
		if (CurrentIsChoice) {
			if (Input.GetKeyDown ("return")) {
				Speak ();
			}
			if (Input.GetKeyDown ("up")) {
				OptionPrevious ();
			}
			if (Input.GetKeyDown ("down")) {
				OptionNext ();
			}
		} else {
			if (Input.GetKeyDown ("return") || Input.GetMouseButtonDown (0)) {
				Speak ();
			}
		}
	}
	

	//the various event's trigger code
	static public void _OnDialogueStartFailed(UDEAEvent e, object Instance=null)
	{if (null != OnDialogueStartFailed)	OnDialogueStartFailed(Instance, e); }

	static public void _OnDialogueStarted(UDEAEvent e, object Instance=null)		
	{if (null != OnDialogueStarted)	OnDialogueStarted(Instance, e); }

	static public void _OnSpeakStart(UDEAEvent e, object Instance=null)			
	{if (null != OnSpeakStart)	OnSpeakStart(Instance, e); }

	static public void _OnSpeakEnd(UDEAEvent e, object Instance=null)			
	{if (null != OnSpeakEnd)	OnSpeakEnd(Instance, e); }

	static public void _OnDialogueEnded(UDEAEvent e, object Instance=null)		
	{if (null != OnDialogueEnded)	OnDialogueEnded(Instance, e); }

	static public void _OnTypewriterModeUpdate(UDEAEvent e, object Instance=null)
	{if (null != OnTypewriterModeUpdate)	OnTypewriterModeUpdate(Instance, e); }

} // csDialogue

public class UDEAEvent : EventArgs {
    private CML data;
 
    public CML Data
    {
        get
        {
            return data;
        }
        set
        {
            data = value;
        }
    }
    
	public UDEAEvent(CML _data = null) {
		if (null == _data)
			_data = new CML();
		data = _data;
	}
}

class NoDialogueDefinedException : Exception
{
    public NoDialogueDefinedException() : base() {}
    public NoDialogueDefinedException(string s) : base(s) {}
    public NoDialogueDefinedException(string s, Exception ex) : base(s, ex) {}
}

class NoActorsDefinedException : Exception
{
    public NoActorsDefinedException() : base() {}
    public NoActorsDefinedException(string s) : base(s) {}
    public NoActorsDefinedException(string s, Exception ex) : base(s, ex) {}
}

class InvalidKeyDefinitionException : Exception
{
    public InvalidKeyDefinitionException() : base() {}
    public InvalidKeyDefinitionException(string s) : base(s) {}
    public InvalidKeyDefinitionException(string s, Exception ex) : base(s, ex) {}
}

class InvalidReqDefinitionException : Exception
{
    public InvalidReqDefinitionException() : base() {}
    public InvalidReqDefinitionException(string s) : base(s) {}
    public InvalidReqDefinitionException(string s, Exception ex) : base(s, ex) {}
}
