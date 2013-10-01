using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/*
	For years now the GameKeys class has been driving the functionality
	of the Unity Dialogue Engine in the background. The use of this class
	is really simple but, once you realize the various applications this
	can be applied to, you soon realize that this class can have an infinite
	number of uses.
	
	The first thing that people noticed is that the GameKeys class can be
	used as an inventory with no added coding. At one point I created an
	entire shop system just by using the GameKeys class.
	
	It is simple, yet useful in more ways than I can even begin to list
	So here is the basic idea behind this class:
	1. "Whenever anything happens, set a 'key' to indicate that it has happened"
	2. "Whenever you want to do something that requires that something else has
	    already happened, simply check if that particular 'key' has ben set"
	    
	That was basically all this class was supposed to do but to make it slightly
	more versatile, instead of making the keys simple boolean values, I instead
	made it a float value. As a result, instead of saying "Do I have gold?"
	you can now say "Do I have 300 gold?" and that simple change opened a world
	of opportunities.
	
	1. 	Do I have the "Found Princess" key?
	2. 	What level should load next? How many "LevelCompleted" keys do I have?
	3. 	To buy an item you need to be level 3 and have 2000 gold... 
		So simply check the "Player level" and "gold" keys...
	4. 	Picked up 1 crossbow? Excellent, add 1 to the "Crossbow" key.
	5. 	Picked up 1 "crossbow ammo"? Excellend, add 20 to the "crossbow_ammo" key
	6. 	Did the player hit the "fire" button? Fine, first check the ammo key to see
		if you should instantiate the arrow or not...
		
	Wee what I mean? You create a single variable and inside it you keep track of
	everything that you do in the game and use the same variable to determine what
	you can and cannot do in the game. A one stop place to manage your entire game
	from. That is the GameKeys class.
	
	Then, I went and created the CMLGameKeys class on top of CML which gives you
	the exact same features as the normal GameKeys class except now it offers you
	even more versatility. Now, every key can have an infinite number of additional
	info imbedded into it. Anything you could need in your game, just add it.
	
	For example, in the past you could have said:
		You found the Sword. So here is one "Sword" key. Enjoy.
	Now you can say:
		You found the Sword. Here is the "Sword" key. List it under the "Weapons"
		categroy when you view your inventory. This item is a one handed weapon
		and modifies your speed with -3 and your strength by 2. It was made in 1803
		by "Local Vendor 12012" in the town of "StatsVille". It adds +3 to the
		"Stone" element stat and offers a 2x bonus to Wood type weapons.
	etc etc etc...
	
	You can now add any number of additional info per key and have it readily available.
	For instance, in the Unity Dialogue engine, you could display your key values
	inside your dialogues by saying this:
		I have <key:sword> Swords in my inventory
	This would have printed:
		I have 1 Swords in my inventory
		
	Now you can say this:
		I have {:sword:qty} swords with a retail value of {:sword:value} {:currency:name} each
	Which would print
		I have 1 swords with a retail value of 80 Gold each
		
	So how do you use the gamekeys class?
	-------------------------------------
	First, create a place to store your values:
		CMLGameKeys AllMyKeys = new CMLGameKeys();
		
	Now, to add a key, simply use one of the three variation of 'Set' functions to store
	your value as either an int, a string or a float value like so:
		AllMyKeys.Seti("Gold", 10);

	By default this will place the key value inside the "value" property of the key. You could
	make this "value" property be a string like so:
		AllMyKeys.Set("profile", "akira")
	...but this will break the DoesHave and DoesNotHave functions and basically make this class
	useless if you intend to do a search on the particular key, but for something that is not
	likely to be searched against (like profile information), the option is there if you want it.
		
	To add info as additional info for the key, simply add a label like so:
		AllMyKeys.Setf("Gold", 8.32, "Dollar Exchange rate")
		AllMyKeys.Seti("profile", 16, "age");
		
	To retrieve a value is just as simple:
		Debug.Log(AllMyKeys.Value("profile") + " is " + AllMyKeys.Valuei("profile", "age") );
	This will print:
		Akira is 18
		
	Finally, to use is for testing for valid actions in your game, let's say the player
	must find the "LevelKey" key to activate the end of level teleporter. You would then
	write your OnCollissionTrigger event and add something like this inside:
		if (AllMyKeys.DoesHave("LevelKeys", AllMyKeys.Valuei("level") )) {
			AllMyKeys.Add("level", 1);
			Application.LoadLevel("Level" + (AllMyKeys.Valuei("level")) );
		}
		
	The DoesHave function tests that you have a certain amount OR MORE of a particular key
	The DoesNotHave function tests you HAVE LESS THAN a certain amount of keys.
	It is very important to understand that distinction.
	
	If you have 100 Gold, the following will be true:
		DoesHave("Gold", 10);
		DoesHave("Gold", 100);
		DoesNotHave("Gold", 101);

	The following will be false:
		DoesHave("Gold", 101);
		DoesNotHave("Gold", 100);
		DoesNotHave("Gold", 10);
		
	So say you are a reporter who wants to infiltrate a cult. To get past the gate, you must
	give your house, your car and all your money to the guy at the ticket office and enter
	the grounds completely broke. You would want to check 2 things:
		1. Did the player give away all his worldly posessions at the ticket office?
		2. Did he go get some more cash before he came to the gate?
	You would test for these two keys like so:
		if (AllMyKeys.DoesHave("DonatedAll", 1) && AllMyKeys.DoesNotHave("Gold", 1))
			Debug.Log("Welcome sap... erm.. I mean 'Brother'");
			
	or like so:
		if (AllMyKeys.DoesHave("Gold", 1))
			Debug.Log("Sorry, we can't let you in with that " + AllMyKeys.Valuei("Gold") + " Gold");
			else
				if (AllMyKeys.DoesNotHave("DonatedAll", 1))
					Debug.Log("Sorry, you need to donate everything you own to us first");
				else
					BeDuped();
	-- HINT --------------------------
	The CML Dialogue Engine automatically creates a static CMLGameKeys variable called GameKeys
	Since it uses that variable for testing what has and what has not yet happened in the game
	you could simply assign your variable to that static variable and have your entire game use
	your AllMyKeys variable, but since it is already being created for you anyways, you might just
	as well use that one directly, instead. So instead of defining your own variable, just use
		UDE.GameKeys

	---- Function List ---------------

	public bool Set  (string nm, string	vl, string fieldname = "value")
	public bool Setf (string nm, float	vl, string fieldname = "value")
	public bool Seti (string nm, int	vl, string fieldname = "value")

	public string Value  (string nm = "", string fieldname = "value")
	public string Valuef (string nm = "", string fieldname = "value")
	public string Valuei (string nm = "", string fieldname = "value")

	public bool Add 	 (string name, float value, string fieldname = "value")
	public bool Subtract (string name, float value, string fieldname = "value")
	public void Remove	 (string name)

	public bool HasExactly	(string name, float amt)
	public bool DoesHave	(string name, float amt)
	public bool DoesNotHave (string name, float amt)

	public void Clear	(bool keepRetain = false)
	public void Protect (string nm, bool protect)

	---- Inherited -------------------

	virtual public void Initialize() 
	virtual public cmlData First
	virtual public cmlData Last 
	virtual public bool LoadFile(string filename) 
	virtual public bool OpenFile(string FileName)
	virtual public bool ParseFile(string data) 
	virtual public bool Load(string PrefName)
	virtual public bool Save(string PrefName)
	virtual public bool SaveFile(string FileName)

	virtual public void ImbedCMLData() 
	virtual public bool Join(CML other)

	virtual public bool AddNode(string data_type, string add_data = "") 
	virtual public bool InsertNode(string data_type, string add_data = "") 
	virtual public bool InsertNode(int index, string data_type, string add_data="") 
	virtual public bool RemoveNode(int index) 
	virtual public bool RemoveCurrentNode() 

	virtual public bool PositionAtNode(int index) 
	virtual public bool PositionAtFirstNode() 
	virtual public bool PositionAtLastNode() 
	virtual public bool PositionAtNextNode()
	virtual public bool PositionAtPreviousNode() 
	virtual public bool PositionAtFirstNodeOfType(string data_type) 
	virtual public bool ContainsANodeOfType(string data_type) 

	virtual public cmlData GetFirstNodeOfType(string data_type) 
	virtual public int GetFirstNodeOfTypei(string data_type) 
	virtual public cmlData GetLastNodeOfType(string data_type) 
	virtual public int GetLastNodeOfTypei(string data_type) 

	virtual public List<cmlData> DataWithField(string field, string val = "") 
	
	virtual public List<cmlData> Children(int index = -1) 
	virtual public List<cmlData> AllChildNodes(int index = -1) 
	virtual public List<int> Childreni(int index = -1) 
	virtual public List<int> AllChildNodesi(int index = -1) 
	virtual public List<cmlData> AllDataOfType(string type_name, int starting_index = 0, string stop_at_data_type = "")
	virtual public List<int> AllDataOfTypei(string type_name, int starting_index = 0, string stop_at_data_type = "")

	---- Properties ------------------

	public float CMLVersion
	public cmlData this [int index] 
	virtual public int Count 
	virtual public float DocumentCMLVersion

*/	
public class CMLGameKeys : CML{

	public bool Setf (string nm, float vl, string fieldname = "value")
	{
		return Set(nm, vl.ToString(), fieldname);
	}	

	public bool Seti (string nm, int vl, string fieldname = "value")
	{
		return Set(nm, vl.ToString(), fieldname);
	}	

	public bool Set (string nm, string vl, string fieldname = "value")
	{
		if (nm == string.Empty)
			return false;
			
		int result = GetFirstNodeOfTypei(nm);
		if (-1 == result) {
			AddNode(nm, fieldname+"="+vl);
			return true;
		}
		Elements[result].Set(fieldname,vl);
		return true;
	}	

	public string Value (string nm = "", string fieldname = "value")
	{
		if (nm == string.Empty) {
			if (null == Node)
				return string.Empty;
			else
				nm = Node["name"];
		}
		//should never happen, but play it safe...
		if (nm == string.Empty)
			return string.Empty;
			
		int result = GetFirstNodeOfTypei(nm);
		if (-1 == result) 
			return string.Empty;

		return Elements[result].String(fieldname);
	}	

	public float Valuef (string nm, string fieldname = "value")
	{
		float f = 0;
		string s = Value(nm, fieldname);
		if ( float.TryParse(s, out f) )
			return f;
			
		return 0;
	}	

	public int Valuei (string nm, string fieldname = "value")
	{
		return (int)Valuef(nm, fieldname);
	}	

	public bool Add (string name, float value, string fieldname = "value")
	{
		if (name == string.Empty)
			return false;
			
		int result = GetFirstNodeOfTypei(name);
		if (-1 == result)
			return Setf(name, value, fieldname);
		
		Elements[result].Set(fieldname, ( Elements[result].Float(fieldname) + value ).ToString() );
		return true;
	}
	public bool Subtract (string name, float value, string fieldname = "value")
	{
		if (name == string.Empty)
			return false;
			
		int result = GetFirstNodeOfTypei(name);
		if (-1 == result)
			return Setf(name, value, fieldname);
		
		Elements[result].Set(fieldname, ( Elements[result].Float(fieldname) - value ).ToString() );
		return true;
	}
	//modify the base remove code so it has the option of retaining protected fields
	override public bool RemoveCurrentNode ()
	{
		if ( null == Node)
			return false;
			
		if (Node["retain"] == "true") {
			Node["value"] = "0";
			return false;
		}
		
		return base.RemoveCurrentNode ();
	}
	public void Remove (string name)
	{
		int result = GetFirstNodeOfTypei(name);
		if (result >= 0)
			RemoveNode( result );
	}
	public void Clear (bool keepRetain = false)
	{
		int x = 0;
		if (keepRetain) {
			for(x =0; x < Count;)
				if ( Elements[x]["retain"] != "true"  ) 
					RemoveNode(x);
				else
					x++;
		} else {
			Elements.Clear ();
		}
	}
	public void Protect (string nm, bool protect)
	{
		Set( nm, protect.ToString(), "protect");
	}
	public bool HasExactly (string name, float amt, string field = "value")
	{
		if (name == string.Empty)
			return false;
			
		int result = GetFirstNodeOfTypei(name);
		if (-1 == result) 
			return false;

		return Elements[result].Float(field) == amt;
	}
	public bool DoesHave (string name, float amt, string field = "value")
	{
		if (name == string.Empty)
			return false;
			
		int result = GetFirstNodeOfTypei(name);
		if (-1 == result) 
			return false;

		return Elements[result].Float(field) >= amt;
	}
	public bool DoesNotHave (string name, float amt, string field = "value")
	{
		if (name == string.Empty) {
			Debug.LogError("No key name specified. Result is meaningless");
			return true;
		}
			
		int result = GetFirstNodeOfTypei(name);
		if (-1 == result) 
			return true;

		return Elements[result].Float(field) < amt;
	}
	
	new public IEnumerator GetEnumerator() {
		return new mbsGKI(Elements);
	}
}

class mbsGKI : IEnumerator {
	List<cmlData> Elements;
	int i = -1;
	
	public mbsGKI(List<cmlData> data) {
		Elements = data;
	}

	public bool MoveNext() {
		if (++i < Elements.Count)
			return true;
		return false;
	}

	public void Reset() {
		i = -1;
	}

	public object Current {	get { return Elements[i];} }

}
