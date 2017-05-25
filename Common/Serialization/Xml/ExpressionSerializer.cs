using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.DI;
using Altus.Suffūz.Serialization;
using System.Runtime.Serialization;
using System.Xml.Linq;
using System.Reflection;
using System.Linq.Expressions;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Collections.ObjectModel;
using System.Collections;

namespace Common.Serialization.Xml
{
    public class ExpressionSerializer : SerializerBase<System.Linq.Expressions.Expression>
    {
        public override void Register(IContainerMappings mappings)
        {
            mappings.Add().Map<ISerializer<System.Linq.Expressions.Expression>, ExpressionSerializer>();
        }

        protected override object OnDeserialize(byte[] source, Type targetType)
        {
            var xml = SerializationContext.Instance.TextEncoding?.GetString(source)
                ?? UTF8Encoding.UTF8.GetString(source);
            return Deserialize(XElement.Parse(xml));
        }

        protected override byte[] OnSerialize(object source)
        {
            if (source is System.Linq.Expressions.Expression)
            {
                var e = source as System.Linq.Expressions.Expression;
                if (e.NodeType != ExpressionType.Lambda)
                    e = Evaluator.PartialEval(e);//TODO: decide should we call PartialEval or not at all?
                return SerializationContext.Instance.TextEncoding?.GetBytes(GenerateXmlFromExpressionCore(e).ToString() )
                    ?? UTF8Encoding.UTF8.GetBytes(GenerateXmlFromExpressionCore(e).ToString());
            }
            else throw new SerializationException("Could not serialize objevct of type " + source.GetType().Name + ".  Type must be System.Linq.aExpressions.Expression.");
        }

        protected override bool OnSupportsFormats(string format)
        {
            return StandardFormats.XML.Equals(format, StringComparison.CurrentCultureIgnoreCase);
        }

        #region Serialization
        /// <summary>
        /// generate XML attributes for these primitive Types.
        /// </summary>
        static readonly Type[] primitiveTypes = new[] { typeof(string), typeof(int), typeof(bool), typeof(ExpressionType) };
        private Dictionary<string, ParameterExpression> parameters = new Dictionary<string, ParameterExpression>();
        private TypeResolver resolver;
        public List<CustomExpressionXmlConverter> Converters { get; private set; }

        public ExpressionSerializer(TypeResolver resolver, IEnumerable<CustomExpressionXmlConverter> converters = null)
        {
            this.resolver = resolver;
            if (converters != null)
                this.Converters = new List<CustomExpressionXmlConverter>(converters);
            else
                Converters = new List<CustomExpressionXmlConverter>();
        }

        public ExpressionSerializer()
        {
            this.resolver = new TypeResolver(AppDomain.CurrentDomain.GetAssemblies(), null);
            Converters = new List<CustomExpressionXmlConverter>();
        }



        /*
         * SERIALIZATION 
         */


        /// <summary>
        /// Uses first applicable custom serializer, then returns.
        /// Does not attempt to use all custom serializers.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        bool TryCustomSerializers(Expression e, out XElement result)
        {
            result = null;
            int i = 0;
            while (i < this.Converters.Count)
            {
                if (this.Converters[i].TrySerialize(e, out result))
                    return true;
                i++;
            }
            return false;
        }


        private object GenerateXmlFromProperty(Type propType, string propName, object value)
        {
            if (primitiveTypes.Contains(propType))
                return GenerateXmlFromPrimitive(propName, value);

            if (propType.Equals(typeof(object)))//expected: caller invokes with value == a ConstantExpression.Value
            {
                return GenerateXmlFromObject(propName, value);
            }
            if (typeof(Expression).IsAssignableFrom(propType))
                return GenerateXmlFromExpression(propName, value as Expression);
            if (value is MethodInfo || propType.Equals(typeof(MethodInfo)))
                return GenerateXmlFromMethodInfo(propName, value as MethodInfo);
            if (value is PropertyInfo || propType.Equals(typeof(PropertyInfo)))
                return GenerateXmlFromPropertyInfo(propName, value as PropertyInfo);
            if (value is FieldInfo || propType.Equals(typeof(FieldInfo)))
                return GenerateXmlFromFieldInfo(propName, value as FieldInfo);
            if (value is ConstructorInfo || propType.Equals(typeof(ConstructorInfo)))
                return GenerateXmlFromConstructorInfo(propName, value as ConstructorInfo);
            if (propType.Equals(typeof(Type)))
                return GenerateXmlFromType(propName, value as Type);
            if (IsIEnumerableOf<Expression>(propType))
                return GenerateXmlFromExpressionList(propName, AsIEnumerableOf<Expression>(value));
            if (IsIEnumerableOf<MemberInfo>(propType))
                return GenerateXmlFromMemberInfoList(propName, AsIEnumerableOf<MemberInfo>(value));
            if (IsIEnumerableOf<ElementInit>(propType))
                return GenerateXmlFromElementInitList(propName, AsIEnumerableOf<ElementInit>(value));
            if (IsIEnumerableOf<MemberBinding>(propType))
                return GenerateXmlFromBindingList(propName, AsIEnumerableOf<MemberBinding>(value));
            throw new NotSupportedException(propName);
        }

        /// <summary>
        /// Called from somewhere on call stack... from ConstantExpression.Value
        /// Modified since original code for this method was incorrectly getting the value as 
        /// .ToString() for non-primitive types, which ExpressionSerializer was 
        /// unable to later parse back into a value (ExpressionSerializer.ParseConstantFromElement).
        /// </summary>
        /// <param name="propName"></param>
        /// <param name="value">ConstantExpression.Value</param>
        /// <returns></returns>
        private object GenerateXmlFromObject(string propName, object value)
        {
            Assembly mscorlib = typeof(string).Assembly;
            object result = null;
            if (value is Type)
                result = GenerateXmlFromTypeCore((Type)value);
            else if (mscorlib.GetTypes().Any(t => t == value.GetType()))
                result = value.ToString();
            //else
            //    throw new ArgumentException(string.Format("Unable to generate XML for value of Type '{0}'.\nType is not recognized.", value.GetType().FullName));
            else
                result = value.ToString();
            return new XElement(propName,
                result);
        }

        /// <summary>
        /// For use with ConstantExpression.Value
        /// </summary>
        /// <param name="xName"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        private object GenerateXmlFromKnownTypes(string xName, object instance, Type knownType)
        {
            //string xml;
            //XElement xelement;
            //dynamic something = instance;

            //if (typeof(IQueryable).IsAssignableFrom(instance.GetType()))
            //{
            //    if (typeof(Query<>).MakeGenericType(knownType).IsAssignableFrom(instance.GetType()))
            //    {
            //        return instance.ToString();
            //    }
            //    something = LinqHelper.CastToGenericEnumerable((IQueryable)instance, knownType);
            //    something = Enumerable.ToArray(something);
            //}
            //Type instanceType = something.GetType();
            //DataContractSerializer serializer = new DataContractSerializer(instanceType, this.resolver.knownTypes);

            //using (MemoryStream ms = new MemoryStream())
            //{
            //    serializer.WriteObject(ms, something);
            //    ms.Position = 0;
            //    StreamReader reader = new StreamReader(ms, Encoding.UTF8);
            //    xml = reader.ReadToEnd();
            //    xelement = new XElement(xName, xml);
            //    return xelement;
            //}
            throw new NotImplementedException();
        }
        private bool IsIEnumerableOf<T>(Type propType)
        {
            if (!propType.IsGenericType)
                return false;
            Type[] typeArgs = propType.GetGenericArguments();
            if (typeArgs.Length != 1)
                return false;
            if (!typeof(T).IsAssignableFrom(typeArgs[0]))
                return false;
            if (!typeof(IEnumerable<>).MakeGenericType(typeArgs).IsAssignableFrom(propType))
                return false;
            return true;
        }
        private bool IsIEnumerableOf(Type enumerableType, Type elementType)
        {
            if (!enumerableType.IsGenericType)
                return false;
            Type[] typeArgs = enumerableType.GetGenericArguments();
            if (typeArgs.Length != 1)
                return false;
            if (!elementType.IsAssignableFrom(typeArgs[0]))
                return false;
            if (!typeof(IEnumerable<>).MakeGenericType(typeArgs).IsAssignableFrom(enumerableType))
                return false;
            return true;
        }


        private IEnumerable<T> AsIEnumerableOf<T>(object value)
        {
            if (value == null)
                return null;
            return (value as IEnumerable).Cast<T>();
        }

        private object GenerateXmlFromElementInitList(string propName, IEnumerable<ElementInit> initializers)
        {
            if (initializers == null)
                initializers = new ElementInit[] { };
            return new XElement(propName,
                from elementInit in initializers
                select GenerateXmlFromElementInitializer(elementInit));
        }

        private object GenerateXmlFromElementInitializer(ElementInit elementInit)
        {
            return new XElement("ElementInit",
                GenerateXmlFromMethodInfo("AddMethod", elementInit.AddMethod),
                GenerateXmlFromExpressionList("Arguments", elementInit.Arguments));
        }

        private object GenerateXmlFromExpressionList(string propName, IEnumerable<Expression> expressions)
        {
            XElement result = new XElement(propName,
                    from expression in expressions
                    select GenerateXmlFromExpressionCore(expression));
            return result;
        }

        private object GenerateXmlFromMemberInfoList(string propName, IEnumerable<MemberInfo> members)
        {
            if (members == null)
                members = new MemberInfo[] { };
            return new XElement(propName,
                   from member in members
                   select GenerateXmlFromProperty(member.GetType(), "Info", member));
        }

        private object GenerateXmlFromBindingList(string propName, IEnumerable<MemberBinding> bindings)
        {
            if (bindings == null)
                bindings = new MemberBinding[] { };
            return new XElement(propName,
                from binding in bindings
                select GenerateXmlFromBinding(binding));
        }

        private object GenerateXmlFromBinding(MemberBinding binding)
        {
            switch (binding.BindingType)
            {
                case MemberBindingType.Assignment:
                    return GenerateXmlFromAssignment(binding as MemberAssignment);
                case MemberBindingType.ListBinding:
                    return GenerateXmlFromListBinding(binding as MemberListBinding);
                case MemberBindingType.MemberBinding:
                    return GenerateXmlFromMemberBinding(binding as MemberMemberBinding);
                default:
                    throw new NotSupportedException(string.Format("Binding type {0} not supported.", binding.BindingType));
            }
        }

        private object GenerateXmlFromMemberBinding(MemberMemberBinding memberMemberBinding)
        {
            return new XElement("MemberMemberBinding",
                GenerateXmlFromProperty(memberMemberBinding.Member.GetType(), "Member", memberMemberBinding.Member),
                GenerateXmlFromBindingList("Bindings", memberMemberBinding.Bindings));
        }


        private object GenerateXmlFromListBinding(MemberListBinding memberListBinding)
        {
            return new XElement("MemberListBinding",
                GenerateXmlFromProperty(memberListBinding.Member.GetType(), "Member", memberListBinding.Member),
                GenerateXmlFromProperty(memberListBinding.Initializers.GetType(), "Initializers", memberListBinding.Initializers));
        }

        private object GenerateXmlFromAssignment(MemberAssignment memberAssignment)
        {
            return new XElement("MemberAssignment",
                GenerateXmlFromProperty(memberAssignment.Member.GetType(), "Member", memberAssignment.Member),
                GenerateXmlFromProperty(memberAssignment.Expression.GetType(), "Expression", memberAssignment.Expression));
        }

        private XElement GenerateXmlFromExpression(string propName, Expression e)
        {
            return new XElement(propName, GenerateXmlFromExpressionCore(e));
        }

        private object GenerateXmlFromType(string propName, Type type)
        {
            return new XElement(propName, GenerateXmlFromTypeCore(type));
        }

        private XElement GenerateXmlFromTypeCore(Type type)
        {
            //vsadov: add detection of VB anon types
            if (type.Name.StartsWith("<>f__") || type.Name.StartsWith("VB$AnonymousType"))
                return new XElement("AnonymousType",
                    new XAttribute("Name", type.FullName),
                    from property in type.GetProperties()
                    select new XElement("Property",
                        new XAttribute("Name", property.Name),
                        GenerateXmlFromTypeCore(property.PropertyType)),
                    new XElement("Constructor",
                            from parameter in type.GetConstructors().First().GetParameters()
                            select new XElement("Parameter",
                                new XAttribute("Name", parameter.Name),
                                GenerateXmlFromTypeCore(parameter.ParameterType))
                    ));

            else
            {
                //vsadov: GetGenericArguments returns args for nongeneric types 
                //like arrays no need to save them.
                if (type.IsGenericType)
                {
                    return new XElement("Type",
                                            new XAttribute("Name", type.GetGenericTypeDefinition().FullName),
                                            from genArgType in type.GetGenericArguments()
                                            select GenerateXmlFromTypeCore(genArgType));
                }
                else
                {
                    return new XElement("Type", new XAttribute("Name", type.FullName));
                }

            }
        }

        private object GenerateXmlFromPrimitive(string propName, object value)
        {
            return new XAttribute(propName, value);
        }

        private object GenerateXmlFromMethodInfo(string propName, MethodInfo methodInfo)
        {
            if (methodInfo == null)
                return new XElement(propName);
            return new XElement(propName,
                        new XAttribute("MemberType", methodInfo.MemberType),
                        new XAttribute("MethodName", methodInfo.Name),
                        GenerateXmlFromType("DeclaringType", methodInfo.DeclaringType),
                        new XElement("Parameters",
                            from param in methodInfo.GetParameters()
                            select GenerateXmlFromType("Type", param.ParameterType)),
                        new XElement("GenericArgTypes",
                            from argType in methodInfo.GetGenericArguments()
                            select GenerateXmlFromType("Type", argType)));
        }

        private object GenerateXmlFromPropertyInfo(string propName, PropertyInfo propertyInfo)
        {
            if (propertyInfo == null)
                return new XElement(propName);
            return new XElement(propName,
                        new XAttribute("MemberType", propertyInfo.MemberType),
                        new XAttribute("PropertyName", propertyInfo.Name),
                        GenerateXmlFromType("DeclaringType", propertyInfo.DeclaringType),
                        new XElement("IndexParameters",
                            from param in propertyInfo.GetIndexParameters()
                            select GenerateXmlFromType("Type", param.ParameterType)));
        }

        private object GenerateXmlFromFieldInfo(string propName, FieldInfo fieldInfo)
        {
            if (fieldInfo == null)
                return new XElement(propName);
            return new XElement(propName,
                        new XAttribute("MemberType", fieldInfo.MemberType),
                        new XAttribute("FieldName", fieldInfo.Name),
                        GenerateXmlFromType("DeclaringType", fieldInfo.DeclaringType));
        }

        private object GenerateXmlFromConstructorInfo(string propName, ConstructorInfo constructorInfo)
        {
            if (constructorInfo == null)
                return new XElement(propName);
            return new XElement(propName,
                        new XAttribute("MemberType", constructorInfo.MemberType),
                        new XAttribute("MethodName", constructorInfo.Name),
                        GenerateXmlFromType("DeclaringType", constructorInfo.DeclaringType),
                        new XElement("Parameters",
                            from param in constructorInfo.GetParameters()
                            select new XElement("Parameter",
                                new XAttribute("Name", param.Name),
                                GenerateXmlFromType("Type", param.ParameterType))));
        }



        #endregion

        #region Deserialization
        /*
	   * DESERIALIZATION 
	   */

        public Expression Deserialize(XElement xml)
        {
            parameters.Clear();
            return ParseExpressionFromXmlNonNull(xml);
        }

        public Expression<TDelegate> Deserialize<TDelegate>(XElement xml)
        {
            Expression e = Deserialize(xml);
            if (e is Expression<TDelegate>)
                return e as Expression<TDelegate>;
            throw new Exception("xml must represent an Expression<TDelegate>");
        }

        private Expression ParseExpressionFromXml(XElement xml)
        {
            if (xml.IsEmpty)
                return null;

            return ParseExpressionFromXmlNonNull(xml.Elements().First());
        }

        private Expression ParseExpressionFromXmlNonNull(XElement xml)
        {
            Expression expression;
            if (TryCustomDeserializers(xml, out expression))
                return expression;

            if (expression != null)
                return expression;
            switch (xml.Name.LocalName)
            {
                case "BinaryExpression":
                    return ParseBinaryExpresssionFromXml(xml);
                case "ConstantExpression":
                case "TypedConstantExpression":
                    return ParseConstantExpressionFromXml(xml);
                case "ParameterExpression":
                    return ParseParameterExpressionFromXml(xml);
                case "LambdaExpression":
                    return ParseLambdaExpressionFromXml(xml);
                case "MethodCallExpression":
                    return ParseMethodCallExpressionFromXml(xml);
                case "UnaryExpression":
                    return ParseUnaryExpressionFromXml(xml);
                case "MemberExpression":
                case "FieldExpression":
                case "PropertyExpression":
                    return ParseMemberExpressionFromXml(xml);
                case "NewExpression":
                    return ParseNewExpressionFromXml(xml);
                case "ListInitExpression":
                    return ParseListInitExpressionFromXml(xml);
                case "MemberInitExpression":
                    return ParseMemberInitExpressionFromXml(xml);
                case "ConditionalExpression":
                    return ParseConditionalExpressionFromXml(xml);
                case "NewArrayExpression":
                    return ParseNewArrayExpressionFromXml(xml);
                case "TypeBinaryExpression":
                    return ParseTypeBinaryExpressionFromXml(xml);
                case "InvocationExpression":
                    return ParseInvocationExpressionFromXml(xml);
                default:
                    throw new NotSupportedException(xml.Name.LocalName);
            }
        }

        /// <summary>
        /// Uses first applicable custom deserializer, then returns.
        /// Does not attempt to use all custom deserializers.
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        private bool TryCustomDeserializers(XElement xml, out Expression result)
        {
            result = null;
            int i = 0;
            while (i < this.Converters.Count)
            {
                if (this.Converters[i].TryDeserialize(xml, out result))
                    return true;
                i++;
            }
            return false;
        }

        private Expression ParseInvocationExpressionFromXml(XElement xml)
        {
            Expression expression = ParseExpressionFromXml(xml.Element("Expression"));
            var arguments = ParseExpressionListFromXml<Expression>(xml, "Arguments");
            return Expression.Invoke(expression, arguments);
        }

        private Expression ParseTypeBinaryExpressionFromXml(XElement xml)
        {
            Expression expression = ParseExpressionFromXml(xml.Element("Expression"));
            Type typeOperand = ParseTypeFromXml(xml.Element("TypeOperand"));
            return Expression.TypeIs(expression, typeOperand);
        }

        private Expression ParseNewArrayExpressionFromXml(XElement xml)
        {
            Type type = ParseTypeFromXml(xml.Element("Type"));
            if (!type.IsArray)
                throw new Exception("Expected array type");
            Type elemType = type.GetElementType();
            var expressions = ParseExpressionListFromXml<Expression>(xml, "Expressions");
            switch (xml.Attribute("NodeType").Value)
            {
                case "NewArrayInit":
                    return Expression.NewArrayInit(elemType, expressions);
                case "NewArrayBounds":
                    return Expression.NewArrayBounds(elemType, expressions);
                default:
                    throw new Exception("Expected NewArrayInit or NewArrayBounds");
            }
        }

        private Expression ParseConditionalExpressionFromXml(XElement xml)
        {
            Expression test = ParseExpressionFromXml(xml.Element("Test"));
            Expression ifTrue = ParseExpressionFromXml(xml.Element("IfTrue"));
            Expression ifFalse = ParseExpressionFromXml(xml.Element("IfFalse"));
            return Expression.Condition(test, ifTrue, ifFalse);
        }

        private Expression ParseMemberInitExpressionFromXml(XElement xml)
        {
            NewExpression newExpression = ParseNewExpressionFromXml(xml.Element("NewExpression").Element("NewExpression")) as NewExpression;
            var bindings = ParseBindingListFromXml(xml, "Bindings").ToArray();
            return Expression.MemberInit(newExpression, bindings);
        }



        private Expression ParseListInitExpressionFromXml(XElement xml)
        {
            NewExpression newExpression = ParseExpressionFromXml(xml.Element("NewExpression")) as NewExpression;
            if (newExpression == null) throw new Exception("Expceted a NewExpression");
            var initializers = ParseElementInitListFromXml(xml, "Initializers").ToArray();
            return Expression.ListInit(newExpression, initializers);
        }

        private Expression ParseNewExpressionFromXml(XElement xml)
        {
            ConstructorInfo constructor = ParseConstructorInfoFromXml(xml.Element("Constructor"));
            var arguments = ParseExpressionListFromXml<Expression>(xml, "Arguments").ToArray();
            var members = ParseMemberInfoListFromXml<MemberInfo>(xml, "Members").ToArray();
            if (members.Length == 0)
                return Expression.New(constructor, arguments);
            return Expression.New(constructor, arguments, members);
        }

        private Expression ParseMemberExpressionFromXml(XElement xml)
        {
            Expression expression = ParseExpressionFromXml(xml.Element("Expression"));
            MemberInfo member = ParseMemberInfoFromXml(xml.Element("Member"));
            return Expression.MakeMemberAccess(expression, member);
        }

        //Expression ParseFieldExpressionFromXml(XElement xml)
        //{
        //    Expression expression = Expression.Field()
        //}

        private MemberInfo ParseMemberInfoFromXml(XElement xml)
        {
            MemberTypes memberType = (MemberTypes)ParseConstantFromAttribute<MemberTypes>(xml, "MemberType");
            switch (memberType)
            {
                case MemberTypes.Field:
                    return ParseFieldInfoFromXml(xml);
                case MemberTypes.Property:
                    return ParsePropertyInfoFromXml(xml);
                case MemberTypes.Method:
                    return ParseMethodInfoFromXml(xml);
                case MemberTypes.Constructor:
                    return ParseConstructorInfoFromXml(xml);
                case MemberTypes.Custom:
                case MemberTypes.Event:
                case MemberTypes.NestedType:
                case MemberTypes.TypeInfo:
                default:
                    throw new NotSupportedException(string.Format("MEmberType {0} not supported", memberType));
            }

        }

        private MemberInfo ParseFieldInfoFromXml(XElement xml)
        {
            string fieldName = (string)ParseConstantFromAttribute<string>(xml, "FieldName");
            Type declaringType = ParseTypeFromXml(xml.Element("DeclaringType"));
            return declaringType.GetField(fieldName);
        }

        private MemberInfo ParsePropertyInfoFromXml(XElement xml)
        {
            string propertyName = (string)ParseConstantFromAttribute<string>(xml, "PropertyName");
            Type declaringType = ParseTypeFromXml(xml.Element("DeclaringType"));
            var ps = from paramXml in xml.Element("IndexParameters").Elements()
                     select ParseTypeFromXml(paramXml);
            return declaringType.GetProperty(propertyName);
        }

        private Expression ParseUnaryExpressionFromXml(XElement xml)
        {
            Expression operand = ParseExpressionFromXml(xml.Element("Operand"));
            MethodInfo method = ParseMethodInfoFromXml(xml.Element("Method"));
            var isLifted = (bool)ParseConstantFromAttribute<bool>(xml, "IsLifted");
            var isLiftedToNull = (bool)ParseConstantFromAttribute<bool>(xml, "IsLiftedToNull");
            var expressionType = (ExpressionType)ParseConstantFromAttribute<ExpressionType>(xml, "NodeType");
            var type = ParseTypeFromXml(xml.Element("Type"));
            // TODO: Why can't we use IsLifted and IsLiftedToNull here?  
            // May need to special case a nodeType if it needs them.
            return Expression.MakeUnary(expressionType, operand, type, method);
        }

        private Expression ParseMethodCallExpressionFromXml(XElement xml)
        {
            Expression instance = ParseExpressionFromXml(xml.Element("Object"));
            MethodInfo method = ParseMethodInfoFromXml(xml.Element("Method"));
            IEnumerable<Expression> arguments = ParseExpressionListFromXml<Expression>(xml, "Arguments");
            if (arguments == null || arguments.Count() == 0)
                arguments = new Expression[0];
            if (instance == null)//static method
            {
                return Expression.Call(method: method, arguments: arguments);
            }
            else
                return Expression.Call(instance, method, arguments);
        }

        private Expression ParseLambdaExpressionFromXml(XElement xml)
        {
            var body = ParseExpressionFromXml(xml.Element("Body"));
            var parameters = ParseExpressionListFromXml<ParameterExpression>(xml, "Parameters");
            var type = ParseTypeFromXml(xml.Element("Type"));
            // We may need to 
            //var lambdaExpressionReturnType = type.GetMethod("Invoke").ReturnType;
            //if (lambdaExpressionReturnType.IsArray)
            //{

            //    type = typeof(IEnumerable<>).MakeGenericType(type.GetElementType());
            //}
            return Expression.Lambda(type, body, parameters);
        }

        private IEnumerable<T> ParseExpressionListFromXml<T>(XElement xml, string elemName) where T : Expression
        {
            IEnumerable<XElement> elements = xml.Elements(elemName).Elements();
            List<T> list = new List<T>();
            foreach (XElement tXml in elements)
            {
                object parsed = ParseExpressionFromXmlNonNull(tXml);
                list.Add((T)parsed);
            }
            return list;
            //return from tXml in xml.Element(elemName).Elements()
            //       select (T)ParseExpressionFromXmlNonNull(tXml);
        }

        private IEnumerable<T> ParseMemberInfoListFromXml<T>(XElement xml, string elemName) where T : MemberInfo
        {
            return from tXml in xml.Element(elemName).Elements()
                   select (T)ParseMemberInfoFromXml(tXml);
        }

        private IEnumerable<ElementInit> ParseElementInitListFromXml(XElement xml, string elemName)
        {
            return from tXml in xml.Element(elemName).Elements()
                   select ParseElementInitFromXml(tXml);
        }

        private ElementInit ParseElementInitFromXml(XElement xml)
        {
            MethodInfo addMethod = ParseMethodInfoFromXml(xml.Element("AddMethod"));
            var arguments = ParseExpressionListFromXml<Expression>(xml, "Arguments");
            return Expression.ElementInit(addMethod, arguments);

        }

        private IEnumerable<MemberBinding> ParseBindingListFromXml(XElement xml, string elemName)
        {
            return from tXml in xml.Element(elemName).Elements()
                   select ParseBindingFromXml(tXml);
        }

        private MemberBinding ParseBindingFromXml(XElement tXml)
        {
            MemberInfo member = ParseMemberInfoFromXml(tXml.Element("Member"));
            switch (tXml.Name.LocalName)
            {
                case "MemberAssignment":
                    Expression expression = ParseExpressionFromXml(tXml.Element("Expression"));
                    return Expression.Bind(member, expression);
                case "MemberMemberBinding":
                    var bindings = ParseBindingListFromXml(tXml, "Bindings");
                    return Expression.MemberBind(member, bindings);
                case "MemberListBinding":
                    var initializers = ParseElementInitListFromXml(tXml, "Initializers");
                    return Expression.ListBind(member, initializers);
            }
            throw new NotImplementedException();
        }


        private Expression ParseParameterExpressionFromXml(XElement xml)
        {
            Type type = ParseTypeFromXml(xml.Element("Type"));
            string name = (string)ParseConstantFromAttribute<string>(xml, "Name");
            //vs: hack
            string id = name + type.FullName;
            if (!parameters.ContainsKey(id))
                parameters.Add(id, Expression.Parameter(type, name));
            return parameters[id];
        }

        private Expression ParseConstantExpressionFromXml(XElement xml)
        {
            Type type = ParseTypeFromXml(xml.Element("Type"));

            //I changed this to handle Linq.EnumerableQuery: 
            //now the return Type may not necessarily match the type parsed from XML,
            dynamic result = ParseConstantFromElement(xml, "Value", type);
            return Expression.Constant(result, result.GetType());
            //return Expression.Constant(result, type);
        }

        private Type ParseTypeFromXml(XElement xml)
        {
            Debug.Assert(xml.Elements().Count() == 1);
            return ParseTypeFromXmlCore(xml.Elements().First());
        }

        private Type ParseTypeFromXmlCore(XElement xml)
        {
            switch (xml.Name.ToString())
            {
                case "Type":
                    return ParseNormalTypeFromXmlCore(xml);
                case "AnonymousType":
                    return ParseAnonymousTypeFromXmlCore(xml);
                default:
                    throw new ArgumentException("Expected 'Type' or 'AnonymousType'");
            }

        }

        private Type ParseNormalTypeFromXmlCore(XElement xml)
        {
            if (!xml.HasElements)
                return resolver.GetType(xml.Attribute("Name").Value);

            var genericArgumentTypes = from genArgXml in xml.Elements()
                                       select ParseTypeFromXmlCore(genArgXml);
            return resolver.GetType(xml.Attribute("Name").Value, genericArgumentTypes);
        }

        private Type ParseAnonymousTypeFromXmlCore(XElement xElement)
        {
            string name = xElement.Attribute("Name").Value;
            var properties = from propXml in xElement.Elements("Property")
                             select new TypeResolver.NameTypePair
                             {
                                 Name = propXml.Attribute("Name").Value,
                                 Type = ParseTypeFromXml(propXml)
                             };
            var ctr_params = from propXml in xElement.Elements("Constructor").Elements("Parameter")
                             select new TypeResolver.NameTypePair
                             {
                                 Name = propXml.Attribute("Name").Value,
                                 Type = ParseTypeFromXml(propXml)
                             };

            return resolver.GetOrCreateAnonymousTypeFor(name, properties.ToArray(), ctr_params.ToArray());
        }

        private Expression ParseBinaryExpresssionFromXml(XElement xml)
        {
            var expressionType = (ExpressionType)ParseConstantFromAttribute<ExpressionType>(xml, "NodeType"); ;
            var left = ParseExpressionFromXml(xml.Element("Left"));
            var right = ParseExpressionFromXml(xml.Element("Right"));

            if (left.Type != right.Type)
                ParseBinaryExpressionConvert(ref left, ref right);

            var isLifted = (bool)ParseConstantFromAttribute<bool>(xml, "IsLifted");
            var isLiftedToNull = (bool)ParseConstantFromAttribute<bool>(xml, "IsLiftedToNull");
            var type = ParseTypeFromXml(xml.Element("Type"));
            var method = ParseMethodInfoFromXml(xml.Element("Method"));
            LambdaExpression conversion = ParseExpressionFromXml(xml.Element("Conversion")) as LambdaExpression;
            if (expressionType == ExpressionType.Coalesce)
                return Expression.Coalesce(left, right, conversion);
            return Expression.MakeBinary(expressionType, left, right, isLiftedToNull, method);
        }

        void ParseBinaryExpressionConvert(ref Expression left, ref Expression right)
        {
            if (left.Type != right.Type)
            {
                UnaryExpression unary;
                LambdaExpression lambda;
                if (right is ConstantExpression)
                {
                    unary = Expression.Convert(left, right.Type);
                    left = unary;
                }
                else //(left is ConstantExpression)				
                {
                    unary = Expression.Convert(right, left.Type);
                    right = unary;
                }
                //lambda = Expression.Lambda(unary);
                //Delegate fn = lambda.Compile();
                //var result = fn.DynamicInvoke(new object[0]);
            }
        }

        private MethodInfo ParseMethodInfoFromXml(XElement xml)
        {
            if (xml.IsEmpty)
                return null;
            string name = (string)ParseConstantFromAttribute<string>(xml, "MethodName");
            Type declaringType = ParseTypeFromXml(xml.Element("DeclaringType"));
            var ps = from paramXml in xml.Element("Parameters").Elements()
                     select ParseTypeFromXml(paramXml);
            var genArgs = from argXml in xml.Element("GenericArgTypes").Elements()
                          select ParseTypeFromXml(argXml);
            return resolver.GetMethod(declaringType, name, ps.ToArray(), genArgs.ToArray());
        }

        private ConstructorInfo ParseConstructorInfoFromXml(XElement xml)
        {
            if (xml.IsEmpty)
                return null;
            Type declaringType = ParseTypeFromXml(xml.Element("DeclaringType"));
            var ps = from paramXml in xml.Element("Parameters").Elements()
                     select ParseParameterFromXml(paramXml);
            ConstructorInfo ci = declaringType.GetConstructor(ps.ToArray());
            return ci;
        }

        private Type ParseParameterFromXml(XElement xml)
        {
            string name = (string)ParseConstantFromAttribute<string>(xml, "Name");
            Type type = ParseTypeFromXml(xml.Element("Type"));
            return type;

        }

        private object ParseConstantFromAttribute<T>(XElement xml, string attrName)
        {
            string objectStringValue = xml.Attribute(attrName).Value;
            if (typeof(Type).IsAssignableFrom(typeof(T)))
                throw new Exception("We should never be encoding Types in attributes now.");
            if (typeof(Enum).IsAssignableFrom(typeof(T)))
                return Enum.Parse(typeof(T), objectStringValue, false);
            return Convert.ChangeType(objectStringValue, typeof(T), default(IFormatProvider));
        }

        private object ParseConstantFromAttribute(XElement xml, string attrName, Type type)
        {
            string objectStringValue = xml.Attribute(attrName).Value;
            if (typeof(Type).IsAssignableFrom(type))
                throw new Exception("We should never be encoding Types in attributes now.");
            if (typeof(Enum).IsAssignableFrom(type))
                return Enum.Parse(type, objectStringValue, false);
            return Convert.ChangeType(objectStringValue, type, default(IFormatProvider));
        }

        /// <summary>
        /// returns object for use in a call to Expression.Constant(object, Type)
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="elemName"></param>
        /// <param name="expectedType"></param>
        /// <returns></returns>
        private object ParseConstantFromElement(XElement xml, string elemName, Type expectedType)
        {

            string objectStringValue = xml.Element(elemName).Value;
            if (typeof(Type).IsAssignableFrom(expectedType))
                return ParseTypeFromXml(xml.Element("Value"));
            if (typeof(Enum).IsAssignableFrom(expectedType))
                return Enum.Parse(expectedType, objectStringValue, false);
            return Convert.ChangeType(objectStringValue, expectedType, default(IFormatProvider));
        }
        #endregion

        #region Evaluation

        public XElement GenerateXmlFromExpressionCore(Expression e)
        {
            XElement replace;
            if (e == null)
                return null;
            else if (TryCustomSerializers(e, out replace))
                return replace;
            else if (e is BinaryExpression)
            {
                return BinaryExpressionToXElement((BinaryExpression)e);
            }

            else if (e is BlockExpression)
            {
                return BlockExpressionToXElement((BlockExpression)e);
            }

            else if (e is ConditionalExpression)
            {
                return ConditionalExpressionToXElement((ConditionalExpression)e);
            }

            else if (e is ConstantExpression)
            {
                return ConstantExpressionToXElement((ConstantExpression)e);
            }

            else if (e is DebugInfoExpression)
            {
                return DebugInfoExpressionToXElement((DebugInfoExpression)e);
            }

            else if (e is DefaultExpression)
            {
                return DefaultExpressionToXElement((DefaultExpression)e);
            }

            else if (e is DynamicExpression)
            {
                return DynamicExpressionToXElement((DynamicExpression)e);
            }

            else if (e is GotoExpression)
            {
                return GotoExpressionToXElement((GotoExpression)e);
            }

            else if (e is IndexExpression)
            {
                return IndexExpressionToXElement((IndexExpression)e);
            }

            else if (e is InvocationExpression)
            {
                return InvocationExpressionToXElement((InvocationExpression)e);
            }

            else if (e is LabelExpression)
            {
                return LabelExpressionToXElement((LabelExpression)e);
            }

            else if (e is LambdaExpression)
            {
                return LambdaExpressionToXElement((LambdaExpression)e);
            }

            else if (e is ListInitExpression)
            {
                return ListInitExpressionToXElement((ListInitExpression)e);
            }

            else if (e is LoopExpression)
            {
                return LoopExpressionToXElement((LoopExpression)e);
            }

            else if (e is MemberExpression)
            {
                return MemberExpressionToXElement((MemberExpression)e);
            }

            else if (e is MemberInitExpression)
            {
                return MemberInitExpressionToXElement((MemberInitExpression)e);
            }

            else if (e is MethodCallExpression)
            {
                return MethodCallExpressionToXElement((MethodCallExpression)e);
            }

            else if (e is NewArrayExpression)
            {
                return NewArrayExpressionToXElement((NewArrayExpression)e);
            }

            else if (e is NewExpression)
            {
                return NewExpressionToXElement((NewExpression)e);
            }

            else if (e is ParameterExpression)
            {
                return ParameterExpressionToXElement((ParameterExpression)e);
            }

            else if (e is RuntimeVariablesExpression)
            {
                return RuntimeVariablesExpressionToXElement((RuntimeVariablesExpression)e);
            }

            else if (e is SwitchExpression)
            {
                return SwitchExpressionToXElement((SwitchExpression)e);
            }

            else if (e is TryExpression)
            {
                return TryExpressionToXElement((TryExpression)e);
            }

            else if (e is TypeBinaryExpression)
            {
                return TypeBinaryExpressionToXElement((TypeBinaryExpression)e);
            }

            else if (e is UnaryExpression)
            {
                return UnaryExpressionToXElement((UnaryExpression)e);
            }
            else
                return null;
        }//end GenerateXmlFromExpressionCore


        internal XElement BinaryExpressionToXElement(BinaryExpression e)
        {
            object value;
            string xName = "BinaryExpression";
            object[] XElementValues = new object[9];
            value = ((BinaryExpression)e).CanReduce;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            value = ((BinaryExpression)e).Right;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.Expression),
                "Right", value ?? string.Empty);
            value = ((BinaryExpression)e).Left;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.Expression),
                "Left", value ?? string.Empty);
            value = ((BinaryExpression)e).Method;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Reflection.MethodInfo),
                "Method", value ?? string.Empty);
            value = ((BinaryExpression)e).Conversion;
            XElementValues[4] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.LambdaExpression),
                "Conversion", value ?? string.Empty);
            value = ((BinaryExpression)e).IsLifted;
            XElementValues[5] = GenerateXmlFromProperty(typeof(System.Boolean),
                "IsLifted", value ?? string.Empty);
            value = ((BinaryExpression)e).IsLiftedToNull;
            XElementValues[6] = GenerateXmlFromProperty(typeof(System.Boolean),
                "IsLiftedToNull", value ?? string.Empty);
            value = ((BinaryExpression)e).NodeType;
            XElementValues[7] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((BinaryExpression)e).Type;
            XElementValues[8] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement BlockExpressionToXElement(BlockExpression e)
        {
            object value;
            string xName = "BlockExpression";
            object[] XElementValues = new object[6];
            value = ((BlockExpression)e).Expressions;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Collections.ObjectModel.ReadOnlyCollection<System.Linq.Expressions.Expression>),
                "Expressions", value ?? string.Empty);
            value = ((BlockExpression)e).Variables;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Collections.ObjectModel.ReadOnlyCollection<System.Linq.Expressions.ParameterExpression>),
                "Variables", value ?? string.Empty);
            value = ((BlockExpression)e).Result;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.Expression),
                "Result", value ?? string.Empty);
            value = ((BlockExpression)e).NodeType;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((BlockExpression)e).Type;
            XElementValues[4] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((BlockExpression)e).CanReduce;
            XElementValues[5] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement ConditionalExpressionToXElement(ConditionalExpression e)
        {
            object value;
            string xName = "ConditionalExpression";
            object[] XElementValues = new object[6];
            value = ((ConditionalExpression)e).NodeType;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((ConditionalExpression)e).Type;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((ConditionalExpression)e).Test;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.Expression),
                "Test", value ?? string.Empty);
            value = ((ConditionalExpression)e).IfTrue;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.Expression),
                "IfTrue", value ?? string.Empty);
            value = ((ConditionalExpression)e).IfFalse;
            XElementValues[4] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.Expression),
                "IfFalse", value ?? string.Empty);
            value = ((ConditionalExpression)e).CanReduce;
            XElementValues[5] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement ConstantExpressionToXElement(ConstantExpression e)
        {
            object value;
            string xName = "ConstantExpression";
            object[] XElementValues = new object[4];
            value = ((ConstantExpression)e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((ConstantExpression)e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((ConstantExpression)e).Value;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Object),
                "Value", value ?? string.Empty);
            value = ((ConstantExpression)e).CanReduce;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement DebugInfoExpressionToXElement(DebugInfoExpression e)
        {
            object value;
            string xName = "DebugInfoExpression";
            object[] XElementValues = new object[9];
            value = ((DebugInfoExpression)e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((DebugInfoExpression)e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((DebugInfoExpression)e).StartLine;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Int32),
                "StartLine", value ?? string.Empty);
            value = ((DebugInfoExpression)e).StartColumn;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Int32),
                "StartColumn", value ?? string.Empty);
            value = ((DebugInfoExpression)e).EndLine;
            XElementValues[4] = GenerateXmlFromProperty(typeof(System.Int32),
                "EndLine", value ?? string.Empty);
            value = ((DebugInfoExpression)e).EndColumn;
            XElementValues[5] = GenerateXmlFromProperty(typeof(System.Int32),
                "EndColumn", value ?? string.Empty);
            value = ((DebugInfoExpression)e).Document;
            XElementValues[6] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.SymbolDocumentInfo),
                "Document", value ?? string.Empty);
            value = ((DebugInfoExpression)e).IsClear;
            XElementValues[7] = GenerateXmlFromProperty(typeof(System.Boolean),
                "IsClear", value ?? string.Empty);
            value = ((DebugInfoExpression)e).CanReduce;
            XElementValues[8] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement DefaultExpressionToXElement(DefaultExpression e)
        {
            object value;
            string xName = "DefaultExpression";
            object[] XElementValues = new object[3];
            value = ((DefaultExpression)e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((DefaultExpression)e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((DefaultExpression)e).CanReduce;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement DynamicExpressionToXElement(DynamicExpression e)
        {
            object value;
            string xName = "DynamicExpression";
            object[] XElementValues = new object[6];
            value = ((DynamicExpression)e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((DynamicExpression)e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((DynamicExpression)e).Binder;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Runtime.CompilerServices.CallSiteBinder),
                "Binder", value ?? string.Empty);
            value = ((DynamicExpression)e).DelegateType;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Type),
                "DelegateType", value ?? string.Empty);
            value = ((DynamicExpression)e).Arguments;
            XElementValues[4] = GenerateXmlFromProperty(typeof(System.Collections.ObjectModel.ReadOnlyCollection<System.Linq.Expressions.Expression>),
                "Arguments", value ?? string.Empty);
            value = ((DynamicExpression)e).CanReduce;
            XElementValues[5] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement GotoExpressionToXElement(GotoExpression e)
        {
            object value;
            string xName = "GotoExpression";
            object[] XElementValues = new object[6];
            value = ((GotoExpression)e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((GotoExpression)e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((GotoExpression)e).Value;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.Expression),
                "Value", value ?? string.Empty);
            value = ((GotoExpression)e).Target;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.LabelTarget),
                "Target", value ?? string.Empty);
            value = ((GotoExpression)e).Kind;
            XElementValues[4] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.GotoExpressionKind),
                "Kind", value ?? string.Empty);
            value = ((GotoExpression)e).CanReduce;
            XElementValues[5] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement IndexExpressionToXElement(IndexExpression e)
        {
            object value;
            string xName = "IndexExpression";
            object[] XElementValues = new object[6];
            value = ((IndexExpression)e).NodeType;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((IndexExpression)e).Type;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((IndexExpression)e).Object;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.Expression),
                "Object", value ?? string.Empty);
            value = ((IndexExpression)e).Indexer;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Reflection.PropertyInfo),
                "Indexer", value ?? string.Empty);
            value = ((IndexExpression)e).Arguments;
            XElementValues[4] = GenerateXmlFromProperty(typeof(System.Collections.ObjectModel.ReadOnlyCollection<System.Linq.Expressions.Expression>),
                "Arguments", value ?? string.Empty);
            value = ((IndexExpression)e).CanReduce;
            XElementValues[5] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement InvocationExpressionToXElement(InvocationExpression e)
        {
            object value;
            string xName = "InvocationExpression";
            object[] XElementValues = new object[5];
            value = ((InvocationExpression)e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((InvocationExpression)e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((InvocationExpression)e).Expression;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.Expression),
                "Expression", value ?? string.Empty);
            value = ((InvocationExpression)e).Arguments;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Collections.ObjectModel.ReadOnlyCollection<System.Linq.Expressions.Expression>),
                "Arguments", value ?? string.Empty);
            value = ((InvocationExpression)e).CanReduce;
            XElementValues[4] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement LabelExpressionToXElement(LabelExpression e)
        {
            object value;
            string xName = "LabelExpression";
            object[] XElementValues = new object[5];
            value = ((LabelExpression)e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((LabelExpression)e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((LabelExpression)e).Target;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.LabelTarget),
                "Target", value ?? string.Empty);
            value = ((LabelExpression)e).DefaultValue;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.Expression),
                "DefaultValue", value ?? string.Empty);
            value = ((LabelExpression)e).CanReduce;
            XElementValues[4] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement LambdaExpressionToXElement(LambdaExpression e)
        {
            object value;
            string xName = "LambdaExpression";
            object[] XElementValues = new object[8];
            value = ((LambdaExpression)e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((LambdaExpression)e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((LambdaExpression)e).Parameters;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Collections.ObjectModel.ReadOnlyCollection<System.Linq.Expressions.ParameterExpression>),
                "Parameters", value ?? string.Empty);
            value = ((LambdaExpression)e).Name;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.String),
                "Name", value ?? string.Empty);
            value = ((LambdaExpression)e).Body;
            XElementValues[4] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.Expression),
                "Body", value ?? string.Empty);
            value = ((LambdaExpression)e).ReturnType;
            XElementValues[5] = GenerateXmlFromProperty(typeof(System.Type),
                "ReturnType", value ?? string.Empty);
            value = ((LambdaExpression)e).TailCall;
            XElementValues[6] = GenerateXmlFromProperty(typeof(System.Boolean),
                "TailCall", value ?? string.Empty);
            value = ((LambdaExpression)e).CanReduce;
            XElementValues[7] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement ListInitExpressionToXElement(ListInitExpression e)
        {
            object value;
            string xName = "ListInitExpression";
            object[] XElementValues = new object[5];
            value = ((ListInitExpression)e).NodeType;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((ListInitExpression)e).Type;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((ListInitExpression)e).CanReduce;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            value = ((ListInitExpression)e).NewExpression;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.NewExpression),
                "NewExpression", value ?? string.Empty);
            value = ((ListInitExpression)e).Initializers;
            XElementValues[4] = GenerateXmlFromProperty(typeof(System.Collections.ObjectModel.ReadOnlyCollection<System.Linq.Expressions.ElementInit>),
                "Initializers", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement LoopExpressionToXElement(LoopExpression e)
        {
            object value;
            string xName = "LoopExpression";
            object[] XElementValues = new object[6];
            value = ((LoopExpression)e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((LoopExpression)e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((LoopExpression)e).Body;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.Expression),
                "Body", value ?? string.Empty);
            value = ((LoopExpression)e).BreakLabel;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.LabelTarget),
                "BreakLabel", value ?? string.Empty);
            value = ((LoopExpression)e).ContinueLabel;
            XElementValues[4] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.LabelTarget),
                "ContinueLabel", value ?? string.Empty);
            value = ((LoopExpression)e).CanReduce;
            XElementValues[5] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement MemberExpressionToXElement(MemberExpression e)
        {
            object value;
            string xName = "MemberExpression";
            object[] XElementValues = new object[5];
            value = ((MemberExpression)e).Member;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Reflection.MemberInfo),
                "Member", value ?? string.Empty);
            value = ((MemberExpression)e).Expression;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.Expression),
                "Expression", value ?? string.Empty);
            value = ((MemberExpression)e).NodeType;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((MemberExpression)e).Type;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((MemberExpression)e).CanReduce;
            XElementValues[4] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement MemberInitExpressionToXElement(MemberInitExpression e)
        {
            object value;
            string xName = "MemberInitExpression";
            object[] XElementValues = new object[5];
            value = ((MemberInitExpression)e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((MemberInitExpression)e).CanReduce;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            value = ((MemberInitExpression)e).NodeType;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((MemberInitExpression)e).NewExpression;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.NewExpression),
                "NewExpression", value ?? string.Empty);
            value = ((MemberInitExpression)e).Bindings;
            XElementValues[4] = GenerateXmlFromProperty(typeof(System.Collections.ObjectModel.ReadOnlyCollection<System.Linq.Expressions.MemberBinding>),
                "Bindings", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement MethodCallExpressionToXElement(MethodCallExpression e)
        {
            object value;
            string xName = "MethodCallExpression";
            object[] XElementValues = new object[6];
            value = ((MethodCallExpression)e).NodeType;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((MethodCallExpression)e).Type;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((MethodCallExpression)e).Method;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Reflection.MethodInfo),
                "Method", value ?? string.Empty);
            value = ((MethodCallExpression)e).Object;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.Expression),
                "Object", value ?? string.Empty);
            value = ((MethodCallExpression)e).Arguments;
            XElementValues[4] = GenerateXmlFromProperty(typeof(System.Collections.ObjectModel.ReadOnlyCollection<System.Linq.Expressions.Expression>),
                "Arguments", value ?? string.Empty);
            value = ((MethodCallExpression)e).CanReduce;
            XElementValues[5] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement NewArrayExpressionToXElement(NewArrayExpression e)
        {
            object value;
            string xName = "NewArrayExpression";
            object[] XElementValues = new object[4];
            value = ((NewArrayExpression)e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((NewArrayExpression)e).Expressions;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Collections.ObjectModel.ReadOnlyCollection<System.Linq.Expressions.Expression>),
                "Expressions", value ?? string.Empty);
            value = ((NewArrayExpression)e).NodeType;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((NewArrayExpression)e).CanReduce;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement NewExpressionToXElement(NewExpression e)
        {
            object value;
            string xName = "NewExpression";
            object[] XElementValues = new object[6];
            value = ((NewExpression)e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((NewExpression)e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((NewExpression)e).Constructor;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Reflection.ConstructorInfo),
                "Constructor", value ?? string.Empty);
            value = ((NewExpression)e).Arguments;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Collections.ObjectModel.ReadOnlyCollection<System.Linq.Expressions.Expression>),
                "Arguments", value ?? string.Empty);
            value = ((NewExpression)e).Members;
            XElementValues[4] = GenerateXmlFromProperty(typeof(System.Collections.ObjectModel.ReadOnlyCollection<System.Reflection.MemberInfo>),
                "Members", value ?? string.Empty);
            value = ((NewExpression)e).CanReduce;
            XElementValues[5] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement ParameterExpressionToXElement(ParameterExpression e)
        {
            object value;
            string xName = "ParameterExpression";
            object[] XElementValues = new object[5];
            value = ((ParameterExpression)e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((ParameterExpression)e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((ParameterExpression)e).Name;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.String),
                "Name", value ?? string.Empty);
            value = ((ParameterExpression)e).IsByRef;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Boolean),
                "IsByRef", value ?? string.Empty);
            value = ((ParameterExpression)e).CanReduce;
            XElementValues[4] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement RuntimeVariablesExpressionToXElement(RuntimeVariablesExpression e)
        {
            object value;
            string xName = "RuntimeVariablesExpression";
            object[] XElementValues = new object[4];
            value = ((RuntimeVariablesExpression)e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((RuntimeVariablesExpression)e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((RuntimeVariablesExpression)e).Variables;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Collections.ObjectModel.ReadOnlyCollection<System.Linq.Expressions.ParameterExpression>),
                "Variables", value ?? string.Empty);
            value = ((RuntimeVariablesExpression)e).CanReduce;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement SwitchExpressionToXElement(SwitchExpression e)
        {
            object value;
            string xName = "SwitchExpression";
            object[] XElementValues = new object[7];
            value = ((SwitchExpression)e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((SwitchExpression)e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((SwitchExpression)e).SwitchValue;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.Expression),
                "SwitchValue", value ?? string.Empty);
            value = ((SwitchExpression)e).Cases;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Collections.ObjectModel.ReadOnlyCollection<System.Linq.Expressions.SwitchCase>),
                "Cases", value ?? string.Empty);
            value = ((SwitchExpression)e).DefaultBody;
            XElementValues[4] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.Expression),
                "DefaultBody", value ?? string.Empty);
            value = ((SwitchExpression)e).Comparison;
            XElementValues[5] = GenerateXmlFromProperty(typeof(System.Reflection.MethodInfo),
                "Comparison", value ?? string.Empty);
            value = ((SwitchExpression)e).CanReduce;
            XElementValues[6] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement TryExpressionToXElement(TryExpression e)
        {
            object value;
            string xName = "TryExpression";
            object[] XElementValues = new object[7];
            value = ((TryExpression)e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((TryExpression)e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((TryExpression)e).Body;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.Expression),
                "Body", value ?? string.Empty);
            value = ((TryExpression)e).Handlers;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Collections.ObjectModel.ReadOnlyCollection<System.Linq.Expressions.CatchBlock>),
                "Handlers", value ?? string.Empty);
            value = ((TryExpression)e).Finally;
            XElementValues[4] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.Expression),
                "Finally", value ?? string.Empty);
            value = ((TryExpression)e).Fault;
            XElementValues[5] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.Expression),
                "Fault", value ?? string.Empty);
            value = ((TryExpression)e).CanReduce;
            XElementValues[6] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement TypeBinaryExpressionToXElement(TypeBinaryExpression e)
        {
            object value;
            string xName = "TypeBinaryExpression";
            object[] XElementValues = new object[5];
            value = ((TypeBinaryExpression)e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((TypeBinaryExpression)e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((TypeBinaryExpression)e).Expression;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.Expression),
                "Expression", value ?? string.Empty);
            value = ((TypeBinaryExpression)e).TypeOperand;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Type),
                "TypeOperand", value ?? string.Empty);
            value = ((TypeBinaryExpression)e).CanReduce;
            XElementValues[4] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method
        internal XElement UnaryExpressionToXElement(UnaryExpression e)
        {
            object value;
            string xName = "UnaryExpression";
            object[] XElementValues = new object[7];
            value = ((UnaryExpression)e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(System.Type),
                "Type", value ?? string.Empty);
            value = ((UnaryExpression)e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.ExpressionType),
                "NodeType", value ?? string.Empty);
            value = ((UnaryExpression)e).Operand;
            XElementValues[2] = GenerateXmlFromProperty(typeof(System.Linq.Expressions.Expression),
                "Operand", value ?? string.Empty);
            value = ((UnaryExpression)e).Method;
            XElementValues[3] = GenerateXmlFromProperty(typeof(System.Reflection.MethodInfo),
                "Method", value ?? string.Empty);
            value = ((UnaryExpression)e).IsLifted;
            XElementValues[4] = GenerateXmlFromProperty(typeof(System.Boolean),
                "IsLifted", value ?? string.Empty);
            value = ((UnaryExpression)e).IsLiftedToNull;
            XElementValues[5] = GenerateXmlFromProperty(typeof(System.Boolean),
                "IsLiftedToNull", value ?? string.Empty);
            value = ((UnaryExpression)e).CanReduce;
            XElementValues[6] = GenerateXmlFromProperty(typeof(System.Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }//end static method

        #endregion
    }

    public static class Evaluator
    {
        /// <summary>
        /// Performs evaluation & replacement of independent sub-trees
        /// </summary>
        /// <param name="expression">The root of the expression tree.</param>
        /// <param name="fnCanBeEvaluated">A function that decides whether a given expression node can be part of the local function.</param>
        /// <returns>A new tree with sub-trees evaluated and replaced.</returns>
        public static Expression PartialEval(Expression expression, Func<Expression, bool> fnCanBeEvaluated)
        {
            var nominator = new Nominator(fnCanBeEvaluated);
            var subtreeEvaluator = new SubtreeEvaluator(nominator.Nominate(expression));
            return subtreeEvaluator.Eval(expression);
        }
        /// <summary>
        /// Performs evaluation & replacement of independent sub-trees
        /// </summary>
        /// <param name="expression">The root of the expression tree.</param>
        /// <returns>A new tree with sub-trees evaluated and replaced.</returns>
        public static Expression PartialEval(Expression expression)
        {
            return PartialEval(expression, Evaluator.CanBeEvaluatedLocally);
        }

        /// <summary>
        /// Anything which involves has a sub-Expression as ParameterExpression, such as a MemberExpression,
        /// will not pass this check.
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        private static bool CanBeEvaluatedLocally(Expression expression)
        {
            return expression.NodeType != ExpressionType.Parameter;
        }
        /// <summary>
        /// Evaluates & replaces sub-trees when first candidate is reached (top-down)
        /// </summary>
        class SubtreeEvaluator : ExpressionVisitor
        {
            HashSet<Expression> candidates;
            internal SubtreeEvaluator(HashSet<Expression> candidates)
            {
                this.candidates = candidates;
            }
            internal Expression Eval(Expression exp)
            {
                return this.Visit(exp);
            }

            /// <summary>
            /// Attempt to evaluate each node upon visiting. If the node is a "candidate" (Nominator),
            /// then we replace the Expression node with its evaluated form.
            /// </summary>
            /// <param name="exp"></param>
            /// <returns></returns>
            public override Expression Visit(Expression exp)
            {
                if (exp == null)
                {
                    return null;
                }
                if (this.candidates.Contains(exp))
                {
                    return this.Evaluate(exp);//immediately returns from depth-first tree traversal.
                                              //so, if it's a BinaryExpression and it's already a candidate, it can be evaluated 
                                              //and immediately returned without visitng the Left, Right child nodes.
                }
                return base.Visit(exp);
                //if it's a BinaryExpression and isn't a candidate, then base.Visit will
                //call VisitBinary, which will attempt to evaluate both child Left, Right nodes.
            }

            private Expression Evaluate(Expression e)
            {
                //we have assumed no parameters required for this Expression
                //see (fnCanBeEvaluated)
                LambdaExpression lambda;
                Delegate fn;
                object result;
                switch (e.NodeType)
                {
                    case ExpressionType.Constant:
                        return e;
                    case ExpressionType.Lambda:
                        //case ExpressionType.Lambda:
                        //    lambda = (LambdaExpression)e;
                        //    fn = lambda.Compile();
                        //    result = fn.DynamicInvoke(null);
                        //    return Expression.Constant(result, lambda.ReturnType);
                        return e;
                    //Decided NOT to return a ConstantExpression of the LambdaExpression itself, nor 
                    //the result of invoking a zero-parameter LambdaExpression.
                    default:
                        lambda = Expression.Lambda(e);
                        fn = lambda.Compile();
                        result = fn.DynamicInvoke(null);
                        return Expression.Constant(result, e.Type);
                }
            }
        }

        /// <summary>
        /// Performs bottom-up analysis to determine which nodes can possibly
        /// be part of an evaluated sub-tree.
        /// </summary>
        class Nominator : ExpressionVisitor
        {
            Func<Expression, bool> fnCanBeEvaluated;
            HashSet<Expression> candidates;
            bool cannotBeEvaluated;

            internal Nominator(Func<Expression, bool> fnCanBeEvaluated)
            {
                this.fnCanBeEvaluated = fnCanBeEvaluated;
            }

            internal HashSet<Expression> Nominate(Expression expression)
            {
                this.candidates = new HashSet<Expression>();
                this.Visit(expression);
                return this.candidates;
            }

            /// <summary>
            /// If a child node cannot be evaluated, then its parent can't either. 
            /// A Expression node will fail to be a candidate if it (or a sub-Expression) has a ParameterExpression
            /// </summary>
            /// <param name="expression"></param>
            /// <returns></returns>
            public override Expression Visit(Expression expression)
            {
                if (expression != null)
                {
                    bool saveCannotBeEvaluated = this.cannotBeEvaluated;
                    this.cannotBeEvaluated = false;
                    base.Visit(expression);//visit all child (sub)-expressions...
                                           //after finished visiting all child expressions:
                    if (!this.cannotBeEvaluated)
                    {
                        if (this.fnCanBeEvaluated(expression))
                        {
                            this.candidates.Add(expression);
                        }
                        else
                        {
                            this.cannotBeEvaluated = true;
                        }
                    }
                    this.cannotBeEvaluated |= saveCannotBeEvaluated;
                }
                return expression;
            }
        }
    }

    public sealed class TypeResolver
    {
        private Dictionary<AnonTypeId, Type> anonymousTypes = new Dictionary<AnonTypeId, Type>();
        private ModuleBuilder moduleBuilder;
        private int anonymousTypeIndex = 0;
        /// <summary>
        /// KnownTypes for DataContractSerializer. Only needs to hold the element type, not the collection or array type.
        /// </summary>
        public ReadOnlyCollection<Type> knownTypes { get; private set; }
        HashSet<Assembly> assemblies = new HashSet<Assembly>
        {
            typeof(ExpressionType).Assembly,
            typeof(string).Assembly,
            typeof(List<>).Assembly,
            //typeof(System.ServiceModel.Channels.Binding).Assembly,
			//typeof(System.Runtime.Serialization.DataContractAttribute).Assembly,
			//typeof(System.Runtime.Serialization.Json.DataContractJsonSerializer).Assembly,			
			//typeof(System.Json.JsonObject).Assembly,
 			//typeof(System.ServiceModel.Description.WebHttpBehavior).Assembly,
            typeof(XElement).Assembly,
            Assembly.GetExecutingAssembly(),
            Assembly.GetEntryAssembly()
        };


        /// <summary>
        /// Relying on the constructor only, to load all possible (including IEnumerable, IQueryable, Nullable, Array) Types 
        /// into memory, may not scale well.
        /// </summary>
        /// <param name="assemblies"></param>
        /// <param name="knownTypes"></param>
        public TypeResolver(IEnumerable<Assembly> @assemblies = null, IEnumerable<Type> @knownTypes = null)
        {
            AssemblyName asmname = new AssemblyName();
            asmname.Name = "AnonymousTypes";
            AssemblyBuilder assemblyBuilder = System.Threading.Thread.GetDomain().DefineDynamicAssembly(asmname, AssemblyBuilderAccess.Run);
            moduleBuilder = assemblyBuilder.DefineDynamicModule("AnonymousTypes");
            if (@assemblies != null)
            {
                foreach (var a in @assemblies)
                    this.assemblies.Add(a);
            }

            var simpleTypes = from t in typeof(System.String).Assembly.GetTypes()
                              where
                              (t.IsPrimitive || t == typeof(System.String) || t.IsEnum)
                              && !(t == typeof(IntPtr) || t == typeof(UIntPtr))
                              select t;

            this.knownTypes = new ReadOnlyCollection<Type>(new List<Type>(simpleTypes.Union(@knownTypes ?? Type.EmptyTypes)));
        }
        public bool HasMappedKnownType(Type input)
        {
            Type knownType;
            return this.HasMappedKnownType(input, out knownType);
        }
        /// <summary>
        /// Checks if the input Type is "mapped" or otherwise somehow related (e.g. Array) to a KnownType found in this.KnownTypes.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="knownType"></param>
        /// <returns></returns>
        public bool HasMappedKnownType(Type input, out Type knownType)//out suggestedType?
        {
            HashSet<Type> copy = new HashSet<Type>(this.knownTypes);//to prevent duplicates.
            knownType = null;
            //suggestedType = null;
            //generic , array , IEnumerable types, IQueryable, Nullable Types...
            foreach (Type existing in this.knownTypes)
            {
                if (input == existing)
                {
                    knownType = existing;
                    //suggestedType = knownType;					
                    return true;
                }
                else if (input == existing.MakeArrayType()
                    || input == typeof(IEnumerable<>).MakeGenericType(existing)
                    || IsIEnumerableOf(input, existing))
                {
                    copy.Add(input);
                    this.knownTypes = new ReadOnlyCollection<Type>(new List<Type>(copy));
                    knownType = existing;
                    //suggestedType = existing.MakeArrayType();
                    return true;
                }
                else if (existing.IsValueType && input == typeof(Nullable<>).MakeGenericType(existing))
                {
                    copy.Add(input);
                    this.knownTypes = new ReadOnlyCollection<Type>(new List<Type>(copy));
                    knownType = existing;
                    //suggestedType = existing;//Nullable.Value instead
                    return true;
                }
            }

            return false;// knownType != null;
        }

        //public static bool IsTypeRelated(Type existing, Type input)
        //{
        //    return existing == input
        //            || (input == existing.MakeArrayType())// || input.IsArray && input.GetElementType() == existing)// |
        //            || (input == typeof(IEnumerable<>).MakeGenericType(existing))
        //            || IsIEnumerableOf(input, existing)
        //            || (existing.IsValueType && input == typeof(Nullable<>).MakeGenericType(existing));
        //}

        //protected virtual Type ResolveTypeFromString(string typeString) { return null; }
        //protected virtual string ResolveStringFromType(Type type) { return null; }

        public Type GetType(string typeName, IEnumerable<Type> genericArgumentTypes)
        {
            return GetType(typeName).MakeGenericType(genericArgumentTypes.ToArray());
        }

        public Type GetType(string typeName)
        {
            Type type;
            if (string.IsNullOrEmpty(typeName))
                throw new ArgumentNullException("typeName");
            #region// First - try all replacers
            //type = ResolveTypeFromString(typeName);
            //type = typeReplacers.Select(f => f(typeName)).FirstOrDefault();
            //if (type != null)
            //    return type;
            #endregion

            // If it's an array name - get the element type and wrap in the array type.
            if (typeName.EndsWith("[]"))
                return this.GetType(typeName.Substring(0, typeName.Length - 2)).MakeArrayType();

            if (knownTypes.Any(k => k.FullName == typeName))
                return knownTypes.First(k => k.FullName == typeName);

            // try all loaded types
            foreach (Assembly assembly in this.assemblies.Where(a => a != null).ToArray())
            {
                type = assembly.GetType(typeName);
                if (type != null)
                    return type;
            }

            // Second - try just plain old Type.GetType()
            type = Type.GetType(typeName, false, true);
            if (type != null)
                return type;

            throw new ArgumentException("Could not find a matching type", typeName);
        }

        internal static string GetNameOfExpression(Expression e)
        {
            string name;
            if (e is LambdaExpression)
                name = typeof(LambdaExpression).Name;
            else if (e is ParameterExpression)
                name = typeof(ParameterExpression).Name;//PrimitiveParameterExpression?
            else if (e is BinaryExpression)
                name = typeof(BinaryExpression).Name;//SimpleBinaryExpression?
            else if (e is MethodCallExpression)
                name = typeof(MethodCallExpression).Name;//MethodCallExpressionN?
            else
                name = e.GetType().Name;

            return name;
        }


        public MethodInfo GetMethod(Type declaringType, string name, Type[] parameterTypes, Type[] genArgTypes)
        {
            var methods = from mi in declaringType.GetMethods()
                          where mi.Name == name
                          select mi;
            foreach (var method in methods)
            {
                // Would be nice to remvoe the try/catch
                try
                {
                    MethodInfo realMethod = method;
                    if (method.IsGenericMethod)
                    {
                        realMethod = method.MakeGenericMethod(genArgTypes);
                    }
                    var methodParameterTypes = realMethod.GetParameters().Select(p => p.ParameterType);
                    if (MatchPiecewise(parameterTypes, methodParameterTypes))
                    {
                        return realMethod;
                    }
                }
                catch (ArgumentException)
                {
                    continue;
                }
            }
            return null;
        }


        private bool MatchPiecewise<T>(IEnumerable<T> first, IEnumerable<T> second)
        {
            T[] firstArray = first.ToArray();
            T[] secondArray = second.ToArray();
            if (firstArray.Length != secondArray.Length)
                return false;
            for (int i = 0; i < firstArray.Length; i++)
                if (!firstArray[i].Equals(secondArray[i]))
                    return false;
            return true;
        }

        //vsadov: need to take ctor parameters too as they do not 
        //necessarily match properties order as returned by GetProperties
        public Type GetOrCreateAnonymousTypeFor(string name, NameTypePair[] properties, NameTypePair[] ctr_params)
        {
            AnonTypeId id = new AnonTypeId(name, properties.Concat(ctr_params));
            if (anonymousTypes.ContainsKey(id))
                return anonymousTypes[id];

            //vsadov: VB anon type. not necessary, just looks better
            string anon_prefix = "<>f__AnonymousType";
            TypeBuilder anonTypeBuilder = moduleBuilder.DefineType(anon_prefix + anonymousTypeIndex++, TypeAttributes.Public | TypeAttributes.Class);

            FieldBuilder[] fieldBuilders = new FieldBuilder[properties.Length];
            PropertyBuilder[] propertyBuilders = new PropertyBuilder[properties.Length];

            for (int i = 0; i < properties.Length; i++)
            {
                fieldBuilders[i] = anonTypeBuilder.DefineField("_generatedfield_" + properties[i].Name, properties[i].Type, FieldAttributes.Private);
                propertyBuilders[i] = anonTypeBuilder.DefineProperty(properties[i].Name, PropertyAttributes.None, properties[i].Type, new Type[0]);
                MethodBuilder propertyGetterBuilder = anonTypeBuilder.DefineMethod("get_" + properties[i].Name, MethodAttributes.Public, properties[i].Type, new Type[0]);
                ILGenerator getterILGenerator = propertyGetterBuilder.GetILGenerator();
                getterILGenerator.Emit(OpCodes.Ldarg_0);
                getterILGenerator.Emit(OpCodes.Ldfld, fieldBuilders[i]);
                getterILGenerator.Emit(OpCodes.Ret);
                propertyBuilders[i].SetGetMethod(propertyGetterBuilder);
            }

            ConstructorBuilder constructorBuilder = anonTypeBuilder.DefineConstructor(MethodAttributes.HideBySig | MethodAttributes.Public | MethodAttributes.Public, CallingConventions.Standard, ctr_params.Select(prop => prop.Type).ToArray());
            ILGenerator constructorILGenerator = constructorBuilder.GetILGenerator();
            for (int i = 0; i < ctr_params.Length; i++)
            {
                constructorILGenerator.Emit(OpCodes.Ldarg_0);
                constructorILGenerator.Emit(OpCodes.Ldarg, i + 1);
                constructorILGenerator.Emit(OpCodes.Stfld, fieldBuilders[i]);
                constructorBuilder.DefineParameter(i + 1, ParameterAttributes.None, ctr_params[i].Name);
            }
            constructorILGenerator.Emit(OpCodes.Ret);

            //TODO - Define ToString() and GetHashCode implementations for our generated Anonymous Types
            //MethodBuilder toStringBuilder = anonTypeBuilder.DefineMethod();
            //MethodBuilder getHashCodeBuilder = anonTypeBuilder.DefineMethod();

            Type anonType = anonTypeBuilder.CreateType();
            anonymousTypes.Add(id, anonType);
            return anonType;
        }

        #region static methods

        public static Type GetElementType(Type collectionType)
        {
            Type ienum = FindIEnumerable(collectionType);
            if (ienum == null) return collectionType;
            return ienum.GetGenericArguments()[0];
        }
        private static Type FindIEnumerable(Type collectionType)
        {
            if (collectionType == null || collectionType == typeof(string))
                return null;
            if (collectionType.IsArray)
                return typeof(IEnumerable<>).MakeGenericType(collectionType.GetElementType());
            if (collectionType.IsGenericType)
            {
                foreach (Type arg in collectionType.GetGenericArguments())
                {
                    Type ienum = typeof(IEnumerable<>).MakeGenericType(arg);
                    if (ienum.IsAssignableFrom(collectionType))
                    {
                        return ienum;
                    }
                }
            }
            Type[] ifaces = collectionType.GetInterfaces();
            if (ifaces != null && ifaces.Length > 0)
            {
                foreach (Type iface in ifaces)
                {
                    Type ienum = FindIEnumerable(iface);
                    if (ienum != null) return ienum;
                }
            }
            if (collectionType.BaseType != null && collectionType.BaseType != typeof(object))
            {
                return FindIEnumerable(collectionType.BaseType);
            }
            return null;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="enumerableType">the candidate enumerable Type in question</param>
        /// <param name="elementType">is the candidate type a IEnumerable of elementType</param>
        /// <returns></returns>
        public static bool IsIEnumerableOf(Type enumerableType, Type elementType)
        {
            if (elementType.MakeArrayType() == enumerableType)
                return true;

            if (!enumerableType.IsGenericType)
                return false;
            Type[] typeArgs = enumerableType.GetGenericArguments();
            if (typeArgs.Length != 1)
                return false;
            if (!elementType.IsAssignableFrom(typeArgs[0]))
                return false;
            if (!typeof(IEnumerable<>).MakeGenericType(typeArgs).IsAssignableFrom(enumerableType))
                return false;
            return true;
        }

        public static bool HasBaseType(Type thisType, Type baseType)
        {
            while (thisType.BaseType != null && thisType.BaseType != typeof(System.Object))
            {
                if (thisType.BaseType == baseType)
                    return true;
                thisType = thisType.BaseType;
            }

            return false;
        }
        public static IEnumerable<Type> GetBaseTypes(Type expectedType)
        {
            List<Type> list = new List<Type>();
            list.Add(expectedType);
            if (expectedType.IsArray)
            {
                expectedType = expectedType.GetElementType();
                list.Add(expectedType);
            }
            else
                list.Add(expectedType.MakeArrayType());

            while (expectedType.BaseType != null && expectedType.BaseType != typeof(System.Object))
            {
                expectedType = expectedType.BaseType;
                list.Add(expectedType);
            }
            return list;
        }
        /// <summary>
        /// For determining KnownType(s) to declare on a DataContract
        /// </summary>
        /// <param name="baseType"></param>
        /// <returns></returns>
        public static List<Type> GetDerivedTypes(Type baseType)
        {
            Assembly a = baseType.Assembly;
            var derived = from anyType in a.GetTypes()
                          where HasBaseType(anyType, baseType)
                          select anyType;
            var list = derived.ToList();
            return list;
        }

        public static bool IsNullableType(Type t)
        {
            return t.IsValueType && t.Name == "Nullable`1";
        }
        public static bool HasInheritedProperty(Type declaringType, PropertyInfo pInfo)
        {
            if (pInfo.DeclaringType != declaringType)
                return true;

            while (declaringType.BaseType != null && declaringType.BaseType != typeof(System.Object))
            {
                foreach (var baseP in declaringType.BaseType.GetProperties())
                {
                    if (baseP.Name == pInfo.Name && baseP.PropertyType == pInfo.PropertyType)
                        return true;
                }
                declaringType = declaringType.BaseType;
            }
            return false;
        }

        public static string ToGenericTypeFullNameString(Type t)
        {
            if (t.FullName == null && t.IsGenericParameter)
                return t.GenericParameterPosition == 0 ? "T" : "T" + t.GenericParameterPosition;

            if (!t.IsGenericType)
                return t.FullName;

            string value = t.FullName.Substring(0, t.FullName.IndexOf('`')) + "<";
            Type[] genericArgs = t.GetGenericArguments();
            List<string> list = new List<string>();
            for (int i = 0; i < genericArgs.Length; i++)
            {
                value += "{" + i + "},";
                string s = ToGenericTypeFullNameString(genericArgs[i]);
                list.Add(s);
            }
            value = value.TrimEnd(',');
            value += ">";
            value = string.Format(value, list.ToArray<string>());
            return value;

        }

        public static string ToGenericTypeNameString(Type t)
        {
            string fullname = ToGenericTypeFullNameString(t);
            fullname = fullname.Substring(fullname.LastIndexOf('.') + 1).TrimEnd('>');
            return fullname;
        }
        #endregion



        #region nested classes
        public class NameTypePair
        {
            public string Name { get; set; }
            public Type Type { get; set; }

            public override int GetHashCode()
            {
                return Name.GetHashCode() + Type.GetHashCode();
            }
            public override bool Equals(object obj)
            {
                if (!(obj is NameTypePair))
                    return false;
                NameTypePair other = obj as NameTypePair;
                return Name.Equals(other.Name) && Type.Equals(other.Type);
            }
        }

        private class AnonTypeId
        {
            public string Name { get; private set; }
            public IEnumerable<NameTypePair> Properties { get; private set; }

            public AnonTypeId(string name, IEnumerable<NameTypePair> properties)
            {
                this.Name = name;
                this.Properties = properties;
            }

            public override int GetHashCode()
            {
                int result = Name.GetHashCode();
                foreach (var ntpair in Properties)
                    result += ntpair.GetHashCode();
                return result;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is AnonTypeId))
                    return false;
                AnonTypeId other = obj as AnonTypeId;
                return (Name.Equals(other.Name)
                    && Properties.SequenceEqual(other.Properties));

            }

        }

        #endregion
    }

    public abstract class CustomExpressionXmlConverter
    {
        public abstract bool TryDeserialize(XElement expressionXml, out Expression e);
        public abstract bool TrySerialize(Expression expression, out XElement x);
    }
}
