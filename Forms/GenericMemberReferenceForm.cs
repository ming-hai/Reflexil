
#region " Imports "
using System;
using System.Collections;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Reflector.CodeModel;
using Reflexil.Utils;
using System.Windows.Forms;
#endregion

namespace Reflexil.Forms
{
	
	partial class GenericMemberReferenceForm<T> : IComparer, IReflectionVisitor where T : MemberReference
	{
		
		#region " Constants "
		const string EXPANDER_NODE_KEY = "|-expander-|";
		#endregion
		
		#region " Fields "
		private T m_selected;
		private Dictionary<object, TreeNode> m_nodes = new Dictionary<object, TreeNode>();
		private Dictionary<IReflectionVisitable, IReflectionVisitable> m_visiteditems = new Dictionary<IReflectionVisitable, IReflectionVisitable>();
		private Dictionary<Type, int> m_orders = new Dictionary<Type, int>();
		#endregion
		
		#region " Properties "
		public MemberReference SelectedItem
		{
			get
			{
				return m_selected;
			}
		}
		#endregion
		
		#region " Events "
		private void TreeView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
		{
			LoadNodeOnDemand(e.Node);
		}
		
		private void TreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
		{
			if (e.Node.Tag != null)
			{
				if (typeof(T).IsAssignableFrom(e.Node.Tag.GetType()))
				{
					m_selected = (T) e.Node.Tag;
					ButOk.Enabled = true;
				}
				else
				{
					ButOk.Enabled = false;
				}
			}
			else
			{
				ButOk.Enabled = false;
			}
		}
		
		private void MemberReferenceForm_Load(Object sender, EventArgs e)
		{
			TreeView.Focus();
		}
		#endregion
		
		#region " Methods "
		public GenericMemberReferenceForm(T selected) : base()
		{
			InitializeComponent();
			
			Text = Text + typeof(T).Name.Replace("Reference", string.Empty).ToLower();
			ImageList.Images.AddStrip(DataManager.GetInstance().GetAllImages());
			
			foreach (IAssembly asm in DataManager.GetInstance().GetReflectorAssemblies())
			{
				AppendRootNode(asm);
			}
			
			m_orders.Add(typeof(AssemblyDefinition), 0);
			m_orders.Add(typeof(TypeDefinition), 1);
			m_orders.Add(typeof(MethodDefinition), 2);
			m_orders.Add(typeof(PropertyDefinition), 3);
			m_orders.Add(typeof(EventDefinition), 3);
			m_orders.Add(typeof(FieldDefinition), 5);
			
			TreeView.TreeViewNodeSorter = this;
			
			ButOk.Enabled = selected != null && SelectItem(selected);
		}
		
		#region " Selection "
		public AssemblyDefinition GetAssemblyDefinitionByNodeName(string name)
		{
			foreach (TreeNode subNode in TreeView.Nodes)
			{
				if (subNode.Text == name)
				{
					if ((subNode.Tag) is IAssembly)
					{
						LoadNodeOnDemand(subNode);
					}
					return ((AssemblyDefinition) subNode.Tag);
				}
			}
			return null;
		}
		
		public string StripGenerics(TypeReference item, string str)
		{
			if ((item) is GenericInstanceType)
			{
				foreach (TypeReference arg in ((GenericInstanceType) item).GenericArguments)
				{
					str = str.Replace(string.Format("<{0}>", arg.FullName), string.Empty);
				}
			}
			return str;
		}
		
		public TypeDefinition GetTypeDefinition(TypeReference item)
		{
			ModuleDefinition moddef = null;
			
			if ((item.Scope) is ModuleDefinition)
			{
				moddef = (ModuleDefinition) item.Scope;
				GetAssemblyDefinitionByNodeName(moddef.Assembly.Name.Name);
			}
			else if ((item.Scope) is AssemblyNameReference)
			{
				AssemblyNameReference anr = (AssemblyNameReference) item.Scope;
				AssemblyDefinition asmdef = GetAssemblyDefinitionByNodeName(anr.Name);
				if (asmdef != null)
				{
					moddef = asmdef.MainModule;
				}
			}
			
			if (moddef != null)
			{
				TypeDefinition typedef = moddef.Types[StripGenerics(item, item.FullName)];
				
				if (typedef != null)
				{
					if (typedef.DeclaringType != null)
					{
						GetTypeDefinition(typedef.DeclaringType);
					}
					LoadNodeOnDemand(m_nodes[moddef]);
					LoadNodeOnDemand(m_nodes[typedef]);
					return typedef;
				}
			}
			
			return null;
		}
		
		public MethodDefinition GetMethodDefinition(MethodReference item)
		{
			TypeDefinition typedef = GetTypeDefinition(item.DeclaringType);
			if (typedef != null)
			{
				ArrayList methods = new ArrayList();
				methods.AddRange(typedef.Constructors);
				methods.AddRange(typedef.Methods);
				foreach (MethodDefinition method in methods)
				{
					if (StripGenerics(typedef, method.ToString()) == StripGenerics(item.DeclaringType, item.ToString()))
					{
						return method;
					}
				}
			}
			return null;
		}
		
		public FieldDefinition GetFieldDefinition(FieldReference item)
		{
			TypeDefinition typedef = GetTypeDefinition(item.DeclaringType);
			if (typedef != null)
			{
				foreach (FieldDefinition field in typedef.Fields)
				{
					if (StripGenerics(typedef, field.ToString()) == StripGenerics(item.DeclaringType, item.ToString()))
					{
						return field;
					}
				}
			}
			return null;
		}
		
		public bool SelectItem(MemberReference item)
		{
			object itemtag = null;
			
			if ((item) is TypeReference)
			{
				itemtag = GetTypeDefinition((TypeReference) item);
			}
			else if ((item) is MethodReference)
			{
				itemtag = GetMethodDefinition((MethodReference) item);
			}
			else if ((item) is FieldReference)
			{
				itemtag = GetFieldDefinition((FieldReference) item);
			}
			
			if (itemtag != null&& m_nodes.ContainsKey(itemtag))
			{
				TreeView.SelectedNode = m_nodes[itemtag];
				m_selected = (T) item;
				return true;
			}
			
			return false;
		}
		#endregion
		
		#region " Cosmetic "
		public int Compare(object x, object y)
		{
			TreeNode xn = (TreeNode) x;
			TreeNode yn = (TreeNode) y;
			
			int result = 0;
			if (xn.Tag != null&& yn.Tag != null)
			{
				if (m_orders.ContainsKey(xn.Tag.GetType()))
				{
					result = m_orders[xn.Tag.GetType()].CompareTo(m_orders[yn.Tag.GetType()]);
				}
			}
			if (result == 0)
			{
				result = xn.Text.CompareTo(yn.Text);
			}
			return result;
		}
		
		private int ScopeOffset(int attributes, int mask, int publicValue, int friendValue, int protectedFriendValue, int protectedValue, int privateValue)
		{
			int[] scopes = new int[] { publicValue, friendValue, protectedFriendValue, protectedValue, privateValue };
			attributes = attributes & mask;
			
			for (int i = 0; i <= scopes.Length - 1; i++)
			{
				if (attributes == scopes[i])
				{
					return i;
				}
			}
			
			return 0;
		}
		
		private EReflectorImages ImageIndex(object obj)
		{
			EReflectorImages offset = EReflectorImages.Empty;
			
			if ((obj) is TypeDefinition)
			{
				TypeDefinition typedef = (TypeDefinition) obj;
				if (typedef.IsEnum)
				{
					offset = EReflectorImages.PublicEnum;
				}
				else if (typedef.IsInterface)
				{
					offset = EReflectorImages.PublicInterface;
				}
				else if (typedef.IsValueType)
				{
					offset = EReflectorImages.PublicStructure;
				}
				else
				{
					offset = EReflectorImages.PublicClass;
				}
				if ((typedef.Attributes & TypeAttributes.VisibilityMask) < TypeAttributes.Public)
				{
                    offset = (EReflectorImages)((int)offset + EReflectorImages.FriendClass - EReflectorImages.PublicClass);
				}
				else
				{
					offset = offset + ScopeOffset(Convert.ToInt32(typedef.Attributes), (int)TypeAttributes.VisibilityMask, (int)TypeAttributes.NestedPublic, (int)TypeAttributes.NestedAssembly, (int)TypeAttributes.NestedFamORAssem, (int)TypeAttributes.NestedFamily, (int)TypeAttributes.NestedPrivate);
				}
			}
			else if ((obj) is PropertyDefinition)
			{
				PropertyDefinition propdef = (PropertyDefinition) obj;
				if (propdef.GetMethod == null)
				{
					if (propdef.SetMethod.IsStatic)
					{
						offset = EReflectorImages.PublicSharedWriteOnlyProperty;
					}
					else
					{
						offset = EReflectorImages.PublicWriteOnlyProperty;
					}
				}
				else if (propdef.SetMethod == null)
				{
					if (propdef.GetMethod.IsStatic)
					{
						offset = EReflectorImages.PublicSharedReadOnlyProperty;
					}
					else
					{
						offset = EReflectorImages.PublicReadOnlyProperty;
					}
				}
				else
				{
					if (propdef.GetMethod.IsStatic)
					{
						offset = EReflectorImages.PublicSharedProperty;
					}
					else
					{
						offset = EReflectorImages.PublicProperty;
					}
				}
			}
			else if ((obj) is MethodDefinition)
			{
				MethodDefinition metdef = (MethodDefinition) obj;
				if (metdef.IsConstructor)
				{
					if (metdef.IsStatic)
					{
						offset = EReflectorImages.PublicSharedConstructor;
					}
					else
					{
						offset = EReflectorImages.PublicConstructor;
					}
				}
				else
				{
					if (metdef.IsVirtual)
					{
						if (metdef.IsStatic)
						{
							offset = EReflectorImages.PublicSharedOverrideMethod;
						}
						else
						{
							offset = EReflectorImages.PublicOverrideMethod;
						}
					}
					else
					{
						if (metdef.IsStatic)
						{
							offset = EReflectorImages.PublicSharedMethod;
						}
						else
						{
							offset = EReflectorImages.PublicMethod;
						}
					}
				}
				offset = offset + ScopeOffset((int)metdef.Attributes, (int)MethodAttributes.MemberAccessMask, (int)MethodAttributes.Public, (int)MethodAttributes.Assem, (int)MethodAttributes.FamORAssem, (int)MethodAttributes.Family, (int)MethodAttributes.Private);
			}
			else if ((obj) is FieldDefinition)
			{
				FieldDefinition field = (FieldDefinition) obj;
				if (field.IsLiteral && field.IsStatic)
				{
					offset = EReflectorImages.PublicEnumValue;
				}
				else
				{
					if (field.IsStatic)
					{
						offset = EReflectorImages.PublicSharedField;
					}
					else
					{
						offset = EReflectorImages.PublicField;
					}
				}
                offset = offset + ScopeOffset((int)field.Attributes, (int)FieldAttributes.FieldAccessMask, (int)FieldAttributes.Public, (int)FieldAttributes.Assembly, (int)FieldAttributes.FamORAssem, (int)FieldAttributes.Family, (int)FieldAttributes.Private);
			}
			else if ((obj) is ModuleDefinition)
			{
				offset = EReflectorImages.Module;
			}
			else if ((obj) is EventDefinition)
			{
				EventDefinition evtdef = (EventDefinition) obj;
				if (evtdef.AddMethod.IsStatic)
				{
					offset = EReflectorImages.PublicSharedEvent;
				}
				else
				{
					offset = EReflectorImages.PublicEvent;
				}
			}
			else if ((obj) is AssemblyDefinition || (obj) is IAssembly)
			{
				offset = EReflectorImages.Assembly;
			}
			else if ((obj) is string)
			{
				offset = EReflectorImages.PublicNamespace;
			}
			
			return offset;
		}
		
		public string DisplayString(object obj)
		{
			if ((obj) is MethodDefinition)
			{
				MethodDefinition metdef = (MethodDefinition) obj;
				return metdef.ToString().Substring(metdef.ToString().IndexOf("::") + 2) + " : " + metdef.ReturnType.ReturnType.ToString();
			}
			else if ((obj) is PropertyDefinition)
			{
				PropertyDefinition propdef = (PropertyDefinition) obj;
				return propdef.ToString().Substring(propdef.ToString().IndexOf("::") + 2) + " : " + propdef.PropertyType.ToString();
			}
			else if ((obj) is FieldDefinition)
			{
				FieldDefinition flddef = (FieldDefinition) obj;
				return flddef.ToString().Substring(flddef.ToString().IndexOf("::") + 2) + " : " + flddef.FieldType.ToString();
			}
			else if ((obj) is ModuleDefinition)
			{
				ModuleDefinition moddef = (ModuleDefinition) obj;
				return moddef.Name;
			}
			else if ((obj) is TypeDefinition)
			{
				TypeDefinition typedef = (TypeDefinition) obj;
				return typedef.Name;
			}
			else if ((obj) is AssemblyDefinition)
			{
				AssemblyDefinition asmdef = (AssemblyDefinition) obj;
				return asmdef.Name.Name;
			}
			else if ((obj) is IAssembly)
			{
				IAssembly iasm = (IAssembly) obj;
				return iasm.Name;
			}
			else if ((obj) is EventDefinition)
			{
				EventDefinition evtdef = (EventDefinition) obj;
				return evtdef.Name;
			}
			else
			{
				return obj.ToString();
			}
		}
		#endregion
		
		#region " Node management "
		private void LoadNodeOnDemand(TreeNode node)
		{
			if (node.Nodes.ContainsKey(EXPANDER_NODE_KEY))
			{
				node.Nodes.RemoveAt(node.Nodes.IndexOfKey(EXPANDER_NODE_KEY));
			}
			if ((node.Tag) is IReflectionVisitable)
			{
				IReflectionVisitable visitable = (IReflectionVisitable) node.Tag;
				if (! m_visiteditems.ContainsKey(visitable))
				{
					visitable.Accept(this);
					m_visiteditems.Add(visitable, visitable);
				}
			}
			else if ((node.Tag) is IAssembly)
			{
				IAssembly iasm = (IAssembly) node.Tag;
				AssemblyDefinition asmdef = DataManager.GetInstance().GetAssemblyDefinition(iasm.Location);
				
				m_nodes.Remove(node.Tag);
				m_nodes.Add(asmdef, node);
				node.Tag = asmdef;
				
				foreach (ModuleDefinition moddef in asmdef.Modules)
				{
					AppendNode(asmdef, moddef, moddef.Types.Count > 0);
				}
			}
		}
		
		private void AppendRootNode(IAssembly root)
		{
            // Prevent dumb users from using regular EXE files loaded in reflector
            if (root.Type != AssemblyType.None)
            {
                TreeNode rootnode = new TreeNode(DisplayString(root));
                rootnode.ImageIndex = (int)ImageIndex(root);
                rootnode.SelectedImageIndex = rootnode.ImageIndex;
                rootnode.Tag = root;
                rootnode.Nodes.Add(EXPANDER_NODE_KEY, EXPANDER_NODE_KEY);
                TreeView.Nodes.Add(rootnode);
                m_nodes.Add(root, rootnode);
            }
		}
		
		private void AppendNode(TreeNode ownernode, object child, bool createExpander)
		{
			if (! m_nodes.ContainsKey(child))
			{
				TreeNode childnode = new TreeNode(DisplayString(child));
				childnode.ImageIndex = (int)ImageIndex(child);
				childnode.SelectedImageIndex = childnode.ImageIndex;
				childnode.Tag = child;
				if (createExpander)
				{
					childnode.Nodes.Add(EXPANDER_NODE_KEY, EXPANDER_NODE_KEY);
				}
				ownernode.Nodes.Add(childnode);
				m_nodes.Add(child, childnode);
			}
		}
		
		private void AppendNode(object owner, object child, bool createExpander)
		{
			TreeNode ownernode = m_nodes[owner];
			AppendNode(ownernode, child, createExpander);
		}
		#endregion
		
		#region " Visitor implementation "
		public void VisitConstructorCollection(ConstructorCollection ctors)
		{
			foreach (MethodDefinition constructor in ctors)
			{
				AppendNode(constructor.DeclaringType, constructor, false);
			}
		}
		
		public void VisitEventDefinitionCollection(EventDefinitionCollection events)
		{
			foreach (EventDefinition evt in events)
			{
				AppendNode(evt.DeclaringType, evt, true);
				if (evt.AddMethod != null)
				{
					AppendNode(evt, evt.AddMethod, false);
				}
				if (evt.RemoveMethod != null)
				{
					AppendNode(evt, evt.RemoveMethod, false);
				}
			}
		}
		
		public void VisitFieldDefinitionCollection(FieldDefinitionCollection fields)
		{
			foreach (FieldDefinition field in fields)
			{
				AppendNode(field.DeclaringType, field, false);
			}
		}
		
		public void VisitMethodDefinitionCollection(MethodDefinitionCollection methods)
		{
			foreach (MethodDefinition method in methods)
			{
				if (! method.IsSpecialName || method.IsConstructor)
				{
					AppendNode(method.DeclaringType, method, false);
				}
			}
		}
		
		public void VisitNestedTypeCollection(NestedTypeCollection nestedTypes)
		{
			foreach (TypeDefinition nestedType in nestedTypes)
			{
				AppendNode(nestedType.DeclaringType, nestedType, true);
			}
		}
		
		public void VisitPropertyDefinitionCollection(PropertyDefinitionCollection properties)
		{
			foreach (PropertyDefinition @property in properties)
			{
				AppendNode(@property.DeclaringType, @property, true);
				if (@property.GetMethod != null)
				{
					AppendNode(@property, @property.GetMethod, false);
				}
				if (@property.SetMethod != null)
				{
					AppendNode(@property, @property.SetMethod, false);
				}
			}
		}
		
		public void VisitTypeDefinitionCollection(TypeDefinitionCollection types)
		{
			foreach (TypeDefinition typedef in types)
			{
                if ((typedef.Attributes & TypeAttributes.VisibilityMask) <= TypeAttributes.Public)
				{
                    AppendNode(typedef.Module, typedef.Namespace, true);
                    AppendNode(typedef.Namespace, typedef, true);
				}
			}
		}
		#endregion
		
		#region " Unimplemented vistor "
		public void VisitEventDefinition(EventDefinition evt)
		{
		}
		
		public void VisitFieldDefinition(FieldDefinition field)
		{
		}
		
		public void VisitModuleDefinition(ModuleDefinition @module)
		{
		}
		
		public void VisitNestedType(TypeDefinition nestedType)
		{
		}
		
		public void VisitPropertyDefinition(PropertyDefinition @property)
		{
		}
		
		public void VisitTypeDefinition(TypeDefinition type)
		{
		}
		
		public void VisitConstructor(MethodDefinition ctor)
		{
		}
		
		public void VisitMethodDefinition(MethodDefinition method)
		{
		}
		
		public void TerminateModuleDefinition(ModuleDefinition @module)
		{
		}
		
		public void VisitExternType(TypeReference externType)
		{
		}
		
		public void VisitExternTypeCollection(ExternTypeCollection externs)
		{
		}
		
		public void VisitInterface(TypeReference interf)
		{
		}
		
		public void VisitInterfaceCollection(InterfaceCollection interfaces)
		{
		}
		
		public void VisitMemberReference(MemberReference member)
		{
		}
		
		public void VisitMemberReferenceCollection(MemberReferenceCollection members)
		{
		}
		
		public void VisitCustomAttribute(CustomAttribute customAttr)
		{
		}
		
		public void VisitCustomAttributeCollection(CustomAttributeCollection customAttrs)
		{
		}
		
		public void VisitGenericParameter(GenericParameter genparam)
		{
		}
		
		public void VisitGenericParameterCollection(GenericParameterCollection genparams)
		{
		}
		
		public void VisitMarshalSpec(MarshalSpec marshalSpec)
		{
		}
		
		public void VisitSecurityDeclaration(SecurityDeclaration secDecl)
		{
		}
		
		public void VisitSecurityDeclarationCollection(SecurityDeclarationCollection secDecls)
		{
		}
		
		public void VisitTypeReference(TypeReference type)
		{
		}
		
		public void VisitTypeReferenceCollection(TypeReferenceCollection refs)
		{
		}
		
		public void VisitOverride(MethodReference ov)
		{
		}
		
		public void VisitOverrideCollection(OverrideCollection meth)
		{
		}
		
		public void VisitParameterDefinition(ParameterDefinition parameter)
		{
		}
		
		public void VisitParameterDefinitionCollection(ParameterDefinitionCollection parameters)
		{
		}
		
		public void VisitPInvokeInfo(PInvokeInfo pinvk)
		{
		}
		#endregion
		
		#endregion
		
	}
	
}

