﻿using System;
using System.Collections.Generic;
using System.Linq;

using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.CSharp.Resolver;

namespace LiveCode.Client
{
	public class LinkedCode
	{
		public string[] Declarations = new string[0];
		public string ValueExpression = "";
	}

	public class TypeCode
	{
		// No namespace support because I can't figureout how to
		// get good TypeDeclarations from SimpleTypes.

		public string Name = "";
		public TypeCode[] Dependencies = new TypeCode[0];
		public string[] Usings = new string[0];
		public string Code = "";

		public string Key {
			get { return Name; }
		}

		public bool HasCode { get { return !string.IsNullOrWhiteSpace (Code); } }

		static readonly Dictionary<string, TypeCode> infos = new Dictionary<string, TypeCode> ();

		public static void Clear ()
		{
			infos.Clear ();
		}

		public static TypeCode Get (string name)
		{			
			var key = name;
			TypeCode ci;
			if (infos.TryGetValue (key, out ci)) {
				return ci;
			}

			ci = new TypeCode {
				Name = name,
			};
			infos [key] = ci;
			return ci;
		}

		public static TypeCode Set (TypeDeclaration typedecl, CSharpAstResolver resolver)
		{
			var ns = typedecl.Parent as NamespaceDeclaration;
			var nsName = ns == null ? "" : ns.FullName;
			var name = typedecl.Name;

			var tc = Get (name);

			var usings =
				resolver.RootNode.Descendants.
				OfType<UsingDeclaration> ().
				Select (x => x.ToString ().Trim ()).
				ToList ();

			if (!string.IsNullOrWhiteSpace (nsName)) {
				var nsUsing = "using " + nsName + ";";
				usings.Add (nsUsing);
			}

			tc.Usings = usings.ToArray ();
			var code = typedecl.ToString ();
			tc.Code = code ?? "";

			var deps = new List<String> ();
			foreach (var d in typedecl.Descendants.OfType<SimpleType> ()) {
				deps.Add (d.Identifier);
			}
			tc.Dependencies = deps.Distinct ().Select (Get).ToArray ();

			return tc;
		}

		public static TypeCode Set (string name, IEnumerable<string> usings, string code, IEnumerable<string> deps)
		{
			var tc = Get (name);

			tc.Usings = usings.ToArray ();
			tc.Code = code ?? "";
			tc.Dependencies = deps.Distinct ().Select (Get).ToArray ();

			return tc;
		}

		void GetDependencies (List<TypeCode> code)
		{
			if (code.Contains (this))
				return;
			code.Add (this);
			foreach (var d in Dependencies) {
				d.GetDependencies (code);
			}
			// Move us to the back
			code.Remove (this);
			code.Add (this);
		}

		public List<TypeCode> AllDependencies {
			get {
				var codes = new List<TypeCode> ();
				GetDependencies (codes);
				return codes;
			}
		}

		public LinkedCode GetLinkedCode ()
		{
			var codes = AllDependencies.Where (x => x.HasCode).ToList ();

			var usings = codes.SelectMany (x => x.Usings).Distinct ().ToList ();

			var suffix = DateTime.UtcNow.Ticks.ToString ();

			var renames =
				codes.
				Select (x => Tuple.Create (
					new System.Text.RegularExpressions.Regex ("\\b" + x.Name + "\\b"),
					x.Name + suffix)).
				ToList ();

			Func<string, string> rename = c => {
				var rc = c;
				foreach (var r in renames) {
					rc = r.Item1.Replace (rc, r.Item2);
				}
				return rc;
			};

			return new LinkedCode {
				ValueExpression = "new " + Name + suffix + "()",
				Declarations = new [] {
					string.Join (Environment.NewLine, usings) + Environment.NewLine +
					string.Join (Environment.NewLine, codes.Select (x => rename (x.Code))),
				}
			};
		}
	}
}

