using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;


namespace SBModManager.Attributes {

	/// <summary>
	/// <strong>Copied from The Conservatory's codebase.</strong>
	/// <para/>
	/// 
	/// A counterpart to <see cref="ExportAttribute"/> which causes the game to automatically locate the node provided
	/// in the constructor of this attribute. Once located, the value of this field or property may be modified such 
	/// that it contains a reference to the located node. (n.b. "may be" because of <see cref="NodeResolutionRule"/>).
	/// <para/>
	/// This is a substitute for adding <see cref="ExportAttribute"/> to a <see cref="Node"/>-derived field or property,
	/// and then setting the reference within the Godot editor.
	/// <para/>
	/// By default, node paths without any path characters (<c>"/"</c>) are treated as <strong>global paths</strong> 
	/// (the kind that begin with the <c>%</c> symbol). This behavior allows the <see cref="CallerMemberNameAttribute"/> to 
	/// function for global nodes, which is the most common use case in The Conservatory. To index a child, use 
	/// <c>"./ChildName"</c>, not <c>"ChildName"</c> (which will be interpreted as <c>"%ChildName"</c>).
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
	public sealed class ImportAttribute : Attribute {


		private static readonly Dictionary<Type, List<ImportAttribute>> CACHE = [];

		/// <summary>
		/// The provided global name in the constructor.
		/// </summary>
		public string PathAsString { get; }

		/// <summary>
		/// The actual <see cref="NodePath"/> instance created for the <see cref="PathAsString"/>.
		/// </summary>
		public NodePath Path { get; }

		/// <summary>
		/// The manner in which to resolve this node.
		/// </summary>
		/// <value>
		/// <see cref="NodeResolutionRule.Required"/>
		/// </value>
		public NodeResolutionRule Resolution { get; }

		/// <summary>
		/// If <see langword="true"/>, imports that use a unique name (i.e. most imports) will also search for a direct child
		/// of the same name, if the unique name is not found. This is extremely niche, hence its inclusion as an explicit
		/// constructor argument.
		/// <para/>
		/// For example, <see cref="AbstractEntityModel"/> uses this with its <see cref="AbstractEntityModel.ViewPointNode"/>,
		/// so that if <c>%ViewPointNode</c> is not found, a hypothetical child named <c>ViewPointNode</c> will be able to take its place.
		/// </summary>
		/// <remarks>
		/// This only affects uniquely named nodes and node paths (paths starting with the <c>%</c> character).
		/// </remarks>
		/// <value>
		/// <see langword="false"/>
		/// </value>
		public bool AllowDirectChildUniqueFallback { get; init; }

		/// <summary>
		/// <strong>Only set on manually instantiated and cached instances of this attribute.</strong>
		/// This is a cached binding to the member that this exists as a part of.
		/// </summary>
		private MemberInfo? _member;

		/// <inheritdoc cref="ImportAttribute"/>
		/// <remarks>
		/// This overload is intended to be used when either no path is required i.e. <c>[Import]</c>, or when an explicitly defined 
		/// manual path is required i.e. <c>[Import("../SiblingName")]</c>, or when both a path and a <see cref="NodeResolutionRule"/>
		/// are required i.e. <c>[Import("../SiblingName", NodeResolutionRule.Optional)]</c>. This is not obvious when viewing 
		/// compiled code as the <see cref="CallerMemberNameAttribute"/> is baked into the compiled IL, hence why this note has been 
		/// provided to you.
		/// </remarks>
		/// <param name="resolution">The manner in which to resolve this node.</param>
		/// <param name="path">The path to the node to resolve. <strong>Note that if this is a string without path characters, it is treated as a unique name.</strong></param>
		public ImportAttribute([CallerMemberName] string path = null!, NodeResolutionRule resolution = NodeResolutionRule.Required) {
			ArgumentException.ThrowIfNullOrWhiteSpace(path);
			if (!Enum.IsDefined(resolution)) throw new ArgumentException($"{nameof(NodeResolutionRule)} value {resolution} is not defined.");
			if (path.Contains(':')) throw new ArgumentException($"The provided path '{path}' contains the ':' character, which causes a {nameof(NodePath)} to index an object's property. This is not valid in the import attribute, which only resolves the object itself.");
			if (!path.Contains('/')) {
				if (!path.StartsWith('%')) {
					path = '%' + path;
				}
			}
			PathAsString = path;
			Path = new NodePath(path);
			Resolution = resolution;
		}

		/// <inheritdoc cref="ImportAttribute"/>
		/// <remarks>
		/// This overload is intended to be used when only a <see cref="NodeResolutionRule"/> is provided in source code, i.e.
		/// <c>[Import(NodeResolutionRule.Optional)]</c>. This is not obvious when viewing compiled code as the 
		/// <see cref="CallerMemberNameAttribute"/> is baked into the compiled IL, hence why this note has been provided to you.
		/// </remarks>
		/// <param name="resolution">The manner in which to resolve this node.</param>
		/// <param name="path">The path to the node to resolve. <strong>Note that if this is a string without path characters, it is treated as a unique name.</strong></param>
		public ImportAttribute(NodeResolutionRule resolution, [CallerMemberName] string path = null!) : this(path, resolution) { }

		/// <summary>
		/// <admonition type="tip">
		/// <strong>This should only be called in <see cref="Node._Ready"/></strong>
		/// Failure to do so is considered an error case.
		/// </admonition>
		/// <para/>
		/// Enumerates over the fields and properties of <paramref name="within"/> and finds those decorated with <see cref="ImportAttribute"/>.
		/// It then uses reflection to set the values of these properties or fields from their path.
		/// </summary>
		/// <remarks>
		/// The following members are considered valid candidates:
		/// <list type="bullet">
		/// <item>Instance fields (including <see langword="readonly"/>),</item>
		/// <item>Properties with a setter (including both <see langword="set"/> and <see langword="init"/>), or</item>
		/// <item><strong>Auto</strong> properties (i.e. <c>{ get; }</c>) with <em>or without</em> a setter (this searches for the backing field <c>&lt;PropertyNameHere&gt;k__BackingField</c>).</item>
		/// </list>
		/// Other member types will raise an <see cref="InvalidOperationException"/>
		/// </remarks>
		/// <param name="within">The node to apply to.</param>
		/// <exception cref="InvalidOperationException">See the remarks of this method. Anything not in this list will raise an exception.</exception>
		[StackTraceHidden]
		public static void ImportAll(Node within) {
			if (!GodotObject.IsInstanceValid(within)) throw new ArgumentNullException(nameof(within), "The provided node is null or has been deleted.");

			Type type = within.GetType();
			if (!CACHE.TryGetValue(type, out List<ImportAttribute>? attributes)) {
				attributes = [];
				CACHE[type] = attributes;

				foreach (MemberInfo mbr in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)) {
					if (mbr.MemberType == MemberTypes.Field || mbr.MemberType == MemberTypes.Property) {
						// As a reminder, GetCustomAttribute instantiates a new instance of the attribute using its metadata. Use this to my advantage.
						ImportAttribute? import = mbr.GetCustomAttribute<ImportAttribute>();
						MemberInfo effectiveMember = mbr;
						if (import != null) {
							if (mbr is FieldInfo field) {
								if (field.IsStatic) {
									throw new InvalidOperationException($"Field {field.Name} (on {type.FullName}) was decorated with {nameof(ImportAttribute)}, but is static. {nameof(ImportAttribute)} cannot be used on a static field or property.");
								}
								if (field.IsLiteral) {
									throw new InvalidOperationException($"Field {field.Name} (on {type.FullName}) was decorated with {nameof(ImportAttribute)}, but is const. You didn't really think that was possible, did you?");
								}
								if (!field.FieldType.IsAssignableTo(typeof(Node))) {
									throw new InvalidOperationException($"Field {field.Name} (on {type.FullName}) was decorated with {nameof(ImportAttribute)}, but the type of the field is not assignable to {nameof(Node)}.");
								}
							} else if (mbr is PropertyInfo property) {
								if (property.GetMethod?.IsStatic ?? property.SetMethod?.IsStatic ?? false) {
									throw new InvalidOperationException($"Property {property.Name} (on {type.FullName}) was decorated with {nameof(ImportAttribute)}, but is static. {nameof(ImportAttribute)} cannot be used on a static field or property.");
								}
								if (!property.PropertyType.IsAssignableTo(typeof(Node))) {
									throw new InvalidOperationException($"Property {property.Name} (on {type.FullName}) was decorated with {nameof(ImportAttribute)}, but the type of the property is not assignable to {nameof(Node)}.");
								}
								if (property.SetMethod == null) {
									FieldInfo? backingField = property.DeclaringType!.GetField($"<{property.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
									if (backingField != null) {
										effectiveMember = backingField;
									} else {
										throw new InvalidOperationException($"Property {property.Name} (on {type.FullName}) was decorated with {nameof(ImportAttribute)}, but has no setter, and a generated backing field could not be found.");
									}
								}
							}
							import._member = effectiveMember;
							attributes.Add(import);
						}
					}
				}
			}

			foreach (ImportAttribute instanceAttribute in attributes) {
				Node? setTo = null;
				string pathString = instanceAttribute.PathAsString;
				bool isUniqueName = pathString[0] == '%';
				if (instanceAttribute.Resolution == NodeResolutionRule.Required) {
					if (instanceAttribute.AllowDirectChildUniqueFallback && isUniqueName) {
						setTo = within.GetNodeOrNull(instanceAttribute.Path);
						setTo ??= within.GetNodeOrNull(pathString[1..]);
						if (setTo == null) {
							throw new InvalidOperationException($"Unable to find node in {within} at path: {instanceAttribute.Path}");
						}
					} else {
						setTo = within.GetNode(instanceAttribute.Path) ?? throw new InvalidOperationException($"Unable to find node in {within} at path: {instanceAttribute.Path}");
					}
				} else {
					// The remaining three involve searching for the existing value.
					setTo = within.GetNodeOrNull(instanceAttribute.Path);
					if (setTo == null && instanceAttribute.AllowDirectChildUniqueFallback && isUniqueName) {
						setTo = within.GetNodeOrNull(instanceAttribute.PathAsString[1..]);
					}
				}

				if (instanceAttribute._member is FieldInfo field) {
					if (instanceAttribute.Resolution == NodeResolutionRule.OptionalDoNotReplace) {
						// DoNotReplace: Keep existing value, fall back to what was found.
						setTo = (field.GetValue(within) as Node) ?? setTo;
					} else if (instanceAttribute.Resolution == NodeResolutionRule.OptionalReplaceOnlyIfFound) {
						// ReplaceOnlyIfFound: Keep what was found, fall back to existing value.
						setTo ??= field.GetValue(within) as Node;
					}
					try {
						field.SetValue(within, setTo);
					} catch (ArgumentException cast) {
						string fieldName = field.Name;
						if (fieldName.StartsWith('<') && fieldName.EndsWith(">k__BackingField")) {
							fieldName = fieldName[1..(fieldName.IndexOf('>') - 1)];
						}
						throw new InvalidOperationException($"{nameof(ImportAttribute)} attempted to load member '{fieldName}' (requires {field.FieldType.FullName}) from NodePath \"{instanceAttribute.PathAsString}\", but it received an instance of [{setTo?.GetType()?.FullName ?? "<null>"}].", cast);
					}
				} else if (instanceAttribute._member is PropertyInfo property) {
					if (instanceAttribute.Resolution == NodeResolutionRule.OptionalDoNotReplace) {
						// DoNotReplace: Keep existing value, fall back to what was found.
						if (property.GetMethod == null) {
							throw new InvalidOperationException($"Property {property.Name} (on {type.FullName}) was decorated with {nameof(ImportAttribute)}, using Resolution Rule {instanceAttribute.Resolution}, but this property doesn't have a getter to read the existing value from.");
						}
						setTo = (property.GetValue(within) as Node) ?? setTo;
					} else if (instanceAttribute.Resolution == NodeResolutionRule.OptionalReplaceOnlyIfFound) {
						// ReplaceOnlyIfFound: Keep what was found, fall back to existing value.
						if (property.GetMethod == null) {
							throw new InvalidOperationException($"Property {property.Name} (on {type.FullName}) was decorated with {nameof(ImportAttribute)}, using Resolution Rule {instanceAttribute.Resolution}, but this property doesn't have a getter to read the existing value from.");
						}
						setTo ??= property.GetValue(within) as Node;
					}
					try {
						property.SetValue(within, setTo);
					} catch (ArgumentException cast) {
						throw new InvalidOperationException($"{nameof(ImportAttribute)} attempted to load member '{property.Name}' (requires {property.PropertyType.FullName}) from NodePath \"{instanceAttribute.PathAsString}\", but it received an instance of [{setTo?.GetType()?.FullName ?? "<null>"}].", cast);
					}
				}
			}
		}

		/// <summary>
		/// The manner in which a node must be resolved.
		/// </summary>
		public enum NodeResolutionRule {

			/// <summary>
			/// The node <strong>must</strong> exist at the specified path. If it does not, a <see cref="MissingNodeException"/> will be raised.
			/// </summary>
			Required,

			/// <summary>
			/// The node is optional. If something has already set the value of this field or property with a non-null, valid node,
			/// then it will be skipped and the existing value kept.
			/// </summary>
			OptionalDoNotReplace,

			/// <summary>
			/// The node is optional. The node will still be searched for. Iff it has been found, any existing value in this field or 
			/// property will be replaced with what was found. Otherwise, the existing value will be left as-is and not be changed.
			/// </summary>
			OptionalReplaceOnlyIfFound,

			/// <summary>
			/// The node is optional. The node will still be searched for. The value of this field or property will be replaced with what
			/// was found, or <see langword="null"/> if nothing was not found. If retaining existing values is more important, use
			/// <see cref="OptionalReplaceOnlyIfFound"/>.
			/// </summary>
			OptionalReplaceAlways,

			/// <summary>
			/// The same as <see cref="OptionalReplaceAlways"/>; the value is always set to what is found, even if what was found is null
			/// and would erase a pre-existing value in the property.
			/// </summary>
			Optional = OptionalReplaceAlways,

		}

	}
}