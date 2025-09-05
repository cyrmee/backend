using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

namespace Application.Common
{
	/// <summary>
	/// Reflection-based object mapper.
	/// <para>
	/// Features:
	/// - Case-insensitive property matching.
	/// - Supports primitives, strings, dates, GUIDs, and nullables.
	/// - Preserves destination values when source is null.
	/// - Recursively maps nested objects and collections.
	/// - Caches mappings for performance.
	/// - Bi-directional mapping.
	/// </para>
	/// <para>
	/// Example:
	/// <code>
	/// var dto = new UserDto { Id = Guid.NewGuid(), Name = "Alice", Tags = new List&lt;string&gt; { "admin" } };
	/// var entity = Mapper.Map&lt;UserDto, User&gt;(dto);
	/// Mapper.Map(dto, entity); // Merge
	/// </code>
	/// </para>
	/// </summary>
	public static class Mapper
	{
		private record PropertyPair(PropertyInfo Source, PropertyInfo Destination);

		private static readonly ConcurrentDictionary<(Type Source, Type Dest), List<PropertyPair>> TypeMaps = new();
		private static readonly ConcurrentDictionary<Type, bool> SimpleTypeCache = new();

		private static readonly ConcurrentDictionary<(Type Source, Type Dest), Dictionary<string, string>>
			CustomMappings = new();

		/// <summary>
		/// Registers custom property mappings for a type pair. Source property name -> Destination property name.
		/// </summary>
		/// <typeparam name="TSource">Source type</typeparam>
		/// <typeparam name="TDestination">Destination type</typeparam>
		/// <param name="mappings">Dictionary of source property name to destination property name</param>
		public static void RegisterCustomMappings<TSource, TDestination>(Dictionary<string, string> mappings)
		{
			CustomMappings[(typeof(TSource), typeof(TDestination))] = mappings;
		}

		/// <summary>
		/// Creates a new destination instance and maps values from source into it.
		/// </summary>
		/// <typeparam name="TSource">Source type</typeparam>
		/// <typeparam name="TDestination">Destination type</typeparam>
		/// <param name="source">Source instance (can be null)</param>
		/// <param name="mergeOnly">If true, preserves existing collections without clearing</param>
		/// <param name="maxDepth">Maximum recursion depth (default 10)</param>
		/// <returns>Newly created and populated destination instance; default if source is null or destination cannot be constructed.</returns>
		public static TDestination Map<TSource, TDestination>(TSource source, bool mergeOnly = false, int maxDepth = 10)
		{
			if (Equals(source, null))
			{
				return default!;
			}

			var destObj = CreateInstance(typeof(TDestination));
			if (destObj is null)
			{
				return default!;
			}

			MapInternal(source, (TDestination)destObj,
				[], mergeOnly, maxDepth);
			return (TDestination)destObj;
		}

		/// <summary>
		/// Maps values from source into an existing destination instance. Keeps existing destination values when the source property is null.
		/// </summary>
		/// <typeparam name="TSource">Source type</typeparam>
		/// <typeparam name="TDestination">Destination type</typeparam>
		/// <param name="source">Source instance</param>
		/// <param name="destination">Destination instance</param>
		/// <param name="mergeOnly">If true, preserves existing collections without clearing</param>
		/// <param name="maxDepth">Maximum recursion depth (default 10)</param>
		public static void Map<TSource, TDestination>(TSource source, TDestination destination, bool mergeOnly = false,
			int maxDepth = 10)
		{
			if (Equals(source, null) || Equals(destination, null))
			{
				return; // If either is null, skip mapping (per requirement)
			}

			MapInternal(source, destination, new Dictionary<(object, Type), object>(),
				mergeOnly, maxDepth);
		}

		private static void MapInternal(object source, object destination, Dictionary<(object, Type), object> visited,
			bool mergeOnly = false, int maxDepth = 10, int currentDepth = 0)
		{
			if (currentDepth > maxDepth)
			{
				throw new InvalidOperationException($"Mapping depth exceeded {maxDepth}. Possible circular reference.");
			}

			var sourceType = source.GetType();
			var destType = destination.GetType();
			var visitedKey = (source, destType);

			// Prevent circular reference infinite loops by tracking (source, destination_type) pairs
			if (!visited.TryAdd(visitedKey, destination))
			{
				return;
			}

			var pairs = GetTypeMap(sourceType, destType);
			foreach (var (srcProp, destProp) in pairs)
			{
				object? srcValue;
				try
				{
					srcValue = srcProp.GetValue(source);
				}
				catch
				{
					continue; // Skip problematic getters
				}

				if (srcValue == null)
				{
					// Keep existing destination value when source is null
					continue;
				}

				var destPropType = destProp.PropertyType;

				if (IsSimpleType(destPropType))
				{
					if (TryConvertValue(srcValue, destPropType, out var converted))
					{
						TrySetValue(destination, destProp, converted);
					}

					continue;
				}

				if (IsEnumerableType(destPropType))
				{
					MapEnumerable(srcValue, destination, destProp, visited, mergeOnly, maxDepth, currentDepth);
					continue;
				}

				// Complex type: map recursively
				object? destCurrent;
				try
				{
					destCurrent = destProp.GetValue(destination);
				}
				catch
				{
					continue;
				}

				if (destCurrent == null)
				{
					destCurrent = CreateInstance(destPropType);
					if (destCurrent == null)
					{
						continue; // Cannot construct destination nested object
					}

					TrySetValue(destination, destProp, destCurrent);
				}

				MapInternal(srcValue, destCurrent, visited, mergeOnly, maxDepth, currentDepth + 1);
			}
		}

		private static void MapEnumerable(object srcValue, object destination, PropertyInfo destProp,
			Dictionary<(object, Type), object> visited, bool mergeOnly = false, int maxDepth = 10, int currentDepth = 0)
		{
			if (srcValue is string)
			{
				// Strings are enumerable of char but should be treated as simple
				TrySetValue(destination, destProp, srcValue);
				return;
			}

			if (srcValue is not IEnumerable srcEnumerable)
			{
				return;
			}

			var destPropType = destProp.PropertyType;
			var (destIsArray, destElementType) = GetEnumerableElementType(destPropType);
			if (destElementType == null)
			{
				return; // Unknown element type, skip
			}

			// Materialize and map elements
			var mappedList = CreateGenericList(destElementType);

			foreach (var srcItem in srcEnumerable)
			{
				if (srcItem == null)
				{
					continue;
				}

				object mappedItem;
				if (IsSimpleType(destElementType))
				{
					if (!TryConvertValue(srcItem, destElementType, out var mapped))
					{
						continue;
					}

					mappedItem = mapped!;
				}
				else
				{
					var newItem = CreateInstance(destElementType);
					if (newItem == null)
					{
						continue;
					}

					MapInternal(srcItem, newItem, visited, mergeOnly, maxDepth, currentDepth + 1);
					mappedItem = newItem;
				}

				_ = MappedListAddMethodCache.GetOrAdd(destElementType, _ => mappedList.GetType().GetMethod("Add")!);
				MappedListAddMethodCache[destElementType].Invoke(mappedList, [mappedItem]);
			}

			if (destIsArray)
			{
				var count = ((ICollection)mappedList).Count;
				var array = Array.CreateInstance(destElementType, count);
				var toArray = MappedListToArrayMethodCache.GetOrAdd(destElementType,
					_ => mappedList.GetType().GetMethod("ToArray")!);
				var tmpArray = (Array)toArray.Invoke(mappedList, [])!;
				Array.Copy(tmpArray, array, count);
				TrySetValue(destination, destProp, array);
			}
			else
			{
				// Prefer updating existing destination collection if present and mutable
				object? existingDest = null;
				try
				{
					existingDest = destProp.GetValue(destination);
				}
				catch
				{
					/* ignore */
				}

				if (existingDest is not null && existingDest is IEnumerable && !mergeOnly)
				{
					// Clear when possible, then add into it.
					TryClearCollection(existingDest);
					CopyListIntoCollection(mappedList, existingDest, destElementType);
				}
				else
				{
					// Construct destination collection and assign
					var destCollection = CreateCollectionInstance(destPropType, destElementType) ?? mappedList;
					CopyListIntoCollection(mappedList, destCollection, destElementType);
					TrySetValue(destination, destProp, destCollection);
				}
			}
		}

		private static readonly ConcurrentDictionary<Type, MethodInfo> MappedListAddMethodCache = new();
		private static readonly ConcurrentDictionary<Type, MethodInfo> MappedListToArrayMethodCache = new();

		private static object CreateGenericList(Type elementType)
		{
			var listType = typeof(List<>).MakeGenericType(elementType);
			return Activator.CreateInstance(listType)!;
		}

		private static void CopyListIntoCollection(object sourceList, object destinationCollection, Type elementType)
		{
			// Try ICollection<T>.Add
			var iCollectionType = typeof(ICollection<>).MakeGenericType(elementType);
			if (iCollectionType.IsInstanceOfType(destinationCollection))
			{
				var addMethod = iCollectionType.GetMethod("Add");
				var enumerator = (IEnumerable)sourceList;
				foreach (var item in enumerator)
				{
					addMethod!.Invoke(destinationCollection, [item]);
				}

				return;
			}

			// Try non-generic IList
			if (destinationCollection is IList nonGenericList)
			{
				foreach (var item in (IEnumerable)sourceList)
				{
					nonGenericList.Add(item);
				}

				return;
			}

			// Fallback: set as-is if assignable
			if (destinationCollection.GetType().IsInstanceOfType(sourceList))
			{
			}
		}

		private static void TryClearCollection(object collection)
		{
			// ICollection<T>.Clear
			var clearMethod = collection.GetType().GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public);
			try
			{
				clearMethod?.Invoke(collection, null);
			}
			catch
			{
				/* ignore */
			}
		}

		private static (bool IsArray, Type? ElementType) GetEnumerableElementType(Type type)
		{
			if (type == typeof(string)) return (false, null);

			if (type.IsArray)
			{
				return (true, type.GetElementType());
			}

			if (!type.IsGenericType) return (false, null);
			var genDef = type.GetGenericTypeDefinition();
			if (typeof(IEnumerable<>).IsAssignableFrom(genDef) ||
				typeof(ICollection<>).IsAssignableFrom(genDef) ||
				typeof(IList<>).IsAssignableFrom(genDef))
			{
				return (false, type.GetGenericArguments()[0]);
			}

			// If not a generic interface directly, search implemented interfaces
			var firstOrDefault = type.GetInterfaces()
				.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
			return firstOrDefault != null ? (false, firstOrDefault.GetGenericArguments()[0]) : (false, null);
		}

		private static bool IsEnumerableType(Type type)
		{
			if (type == typeof(string)) return false;
			return typeof(IEnumerable).IsAssignableFrom(type);
		}

		private static void TrySetValue(object destination, PropertyInfo destProp, object? value)
		{
			try
			{
				destProp.SetValue(destination, value);
			}
			catch
			{
				// ignore write failures
			}
		}

		private static List<PropertyPair> GetTypeMap(Type sourceType, Type destType)
		{
			return TypeMaps.GetOrAdd((sourceType, destType), key => BuildTypeMap(key.Source, key.Dest));
		}

		private static List<PropertyPair> BuildTypeMap(Type sourceType, Type destType)
		{
			const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

			var sourceProps = sourceType
				.GetProperties(flags)
				.Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
				.ToArray();

			var destProps = destType
				.GetProperties(flags)
				.Where(p => p.CanWrite && p.GetIndexParameters().Length == 0)
				.ToArray();

			var sourceMap = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
			foreach (var sp in sourceProps)
			{
				sourceMap[sp.Name] = sp;
			}

			var destMap = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
			foreach (var dp in destProps)
			{
				destMap[dp.Name] = dp;
			}

			var pairs = new List<PropertyPair>(Math.Min(sourceProps.Length, destProps.Length));

			// Check for custom mappings first
			if (CustomMappings.TryGetValue((sourceType, destType), out var customMap))
			{
				foreach (var (srcName, destName) in customMap)
				{
					if (sourceMap.TryGetValue(srcName, out var sp) && destMap.TryGetValue(destName, out var dp))
					{
						pairs.Add(new PropertyPair(sp, dp));
					}
				}
			}
			else
			{
				// Default case-insensitive matching
				foreach (var sp in sourceProps)
				{
					if (destMap.TryGetValue(sp.Name, out var dp))
					{
						pairs.Add(new PropertyPair(sp, dp));
					}
				}
			}

			return pairs;
		}

		private static bool IsSimpleType(Type type)
		{
			return SimpleTypeCache.GetOrAdd(type, t =>
			{
				var u = Nullable.GetUnderlyingType(t) ?? t;
				if (u.IsEnum) return true; // allow enums as simple

				return u.IsPrimitive
					   || u == typeof(string)
					   || u == typeof(decimal)
					   || u == typeof(DateTime)
					   || u == typeof(DateTimeOffset)
					   || u == typeof(Guid)
					   || u == typeof(TimeSpan);
			});
		}

		private static bool TryConvertValue(object? value, Type destinationType, out object? converted)
		{
			var targetType = Nullable.GetUnderlyingType(destinationType) ?? destinationType;

			try
			{
				if (value == null)
				{
					converted = null;
					return !destinationType.IsValueType || Nullable.GetUnderlyingType(destinationType) != null;
				}

				var valueType = value.GetType();

				if (targetType.IsAssignableFrom(valueType))
				{
					converted = value;
					return true;
				}

				// Special cases: Guid, DateTime, DateTimeOffset, TimeSpan from string
				if (targetType == typeof(Guid))
				{
					if (value is string s && Guid.TryParse(s, out var g))
					{
						converted = g;
						return true;
					}
				}
				else if (targetType == typeof(DateTime))
				{
					if (value is string s && DateTime.TryParse(s, CultureInfo.InvariantCulture,
							DateTimeStyles.RoundtripKind, out var dt))
					{
						converted = dt;
						return true;
					}
				}
				else if (targetType == typeof(DateTimeOffset))
				{
					if (value is string s && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
							DateTimeStyles.RoundtripKind, out var dto))
					{
						converted = dto;
						return true;
					}
				}
				else if (targetType == typeof(TimeSpan))
				{
					if (value is string s && TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out var ts))
					{
						converted = ts;
						return true;
					}
				}

				if (value is IConvertible && (targetType.IsPrimitive || targetType == typeof(decimal)))
				{
					converted = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
					return true;
				}

				// Enum conversions
				if (targetType.IsEnum)
				{
					if (value is string es && Enum.TryParse(targetType, es, true, out var ev))
					{
						converted = ev;
						return true;
					}

					if (IsNumericType(value.GetType()))
					{
						converted = Enum.ToObject(targetType, value);
						return true;
					}
				}
			}
			catch
			{
				// ignore and fall through
			}

			converted = null;
			return false;
		}

		private static bool IsNumericType(Type t)
		{
			var u = Nullable.GetUnderlyingType(t) ?? t;
			return u == typeof(byte) || u == typeof(sbyte) || u == typeof(short) || u == typeof(ushort)
				   || u == typeof(int) || u == typeof(uint) || u == typeof(long) || u == typeof(ulong)
				   || u == typeof(float) || u == typeof(double) || u == typeof(decimal);
		}

		private static object? CreateInstance(Type type)
		{
			try
			{
				if (type.IsInterface || type.IsAbstract)
				{
					// Try List<T> for collection interfaces
					if (type.IsGenericType)
					{
						var genDef = type.GetGenericTypeDefinition();
						if (genDef == typeof(IEnumerable<>) || genDef == typeof(ICollection<>) ||
							genDef == typeof(IList<>) || genDef == typeof(IReadOnlyCollection<>) ||
							genDef == typeof(IReadOnlyList<>))
						{
							var elem = type.GetGenericArguments()[0];
							var listType = typeof(List<>).MakeGenericType(elem);
							return Activator.CreateInstance(listType);
						}
					}

					return null;
				}

				// Prefer public parameterless, but allow non-public default ctor
				return Activator.CreateInstance(type, nonPublic: true);
			}
			catch
			{
				return null;
			}
		}

		private static object? CreateCollectionInstance(Type collectionType, Type elementType)
		{
			if (collectionType.IsArray)
			{
				return Array.CreateInstance(elementType, 0);
			}

			if (collectionType.IsInterface || collectionType.IsAbstract)
			{
				// Use List<T> for common interfaces
				var listType = typeof(List<>).MakeGenericType(elementType);
				if (collectionType.IsAssignableFrom(listType))
				{
					return Activator.CreateInstance(listType);
				}
			}
			else
			{
				// Try parameterless constructor
				try
				{
					var instance = Activator.CreateInstance(collectionType, nonPublic: true);
					if (instance != null)
					{
						return instance;
					}
				}
				catch
				{
					/* ignore */
				}
			}

			// Fallback to List<T>
			return Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
		}


	}
}