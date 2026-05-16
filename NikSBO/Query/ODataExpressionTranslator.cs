using System;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;

namespace NikSBO.Query
{
    /// <summary>
    /// Traduce un árbol de expresiones C# (Expression&lt;Func&lt;T, bool&gt;&gt;) a una cláusula
    /// $filter de OData. Lo usa <see cref="B1Query{T}.Where(Expression{Func{T,bool}})"/> para
    /// ofrecer queries tipadas sin cadenas mágicas.
    /// </summary>
    internal class ODataExpressionTranslator : ExpressionVisitor
    {
        private readonly StringBuilder _sb = new StringBuilder();

        /// <summary>
        /// Punto de entrada. Recibe el cuerpo del lambda (predicate.Body) y devuelve la
        /// cláusula OData lista para meter en $filter, p.ej. "(CardCode eq 'C001')".
        /// </summary>
        public string Translate(Expression expression)
        {
            _sb.Clear();
            Visit(expression);
            return _sb.ToString();
        }

        // ---- Operadores binarios: ==, !=, <, >, <=, >=, &&, ||
        protected override Expression VisitBinary(BinaryExpression node)
        {
            _sb.Append('(');
            Visit(node.Left);
            _sb.Append(' ').Append(GetODataOp(node.NodeType)).Append(' ');
            Visit(node.Right);
            _sb.Append(')');
            return node;
        }

        // ---- Acceso a propiedad: x.CardCode  o  miVariableCapturada
        protected override Expression VisitMember(MemberExpression node)
        {
            // Caso 1: la raíz del acceso es el parámetro del lambda → es el nombre del campo en SAP.
            // Importante: asumimos que el nombre de la propiedad C# coincide con el nombre del
            // campo en el Service Layer. Para campos UDF (U_*) o renombrados habría que mirar
            // un atributo en el modelo (futura mejora).
            if (node.Expression is ParameterExpression)
            {
                _sb.Append(node.Member.Name);
                return node;
            }

            // Caso 2: cualquier otra cosa — variable local, propiedad de this, expresión más
            // compleja… Lo más fácil es compilar el sub-árbol y obtener su valor en runtime.
            // Esto cubre closures como:  var x = "Pepe"; .Where(bp => bp.CardName == x)
            var value = Expression.Lambda(node).Compile().DynamicInvoke();
            AppendLiteral(value);
            return node;
        }

        // ---- Literales: "C001", 42, null
        protected override Expression VisitConstant(ConstantExpression node)
        {
            AppendLiteral(node.Value);
            return node;
        }

        // ---- Negación: !bp.IsActive
        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.NodeType == ExpressionType.Not)
            {
                _sb.Append("not (");
                Visit(node.Operand);
                _sb.Append(')');
                return node;
            }
            return base.VisitUnary(node);
        }

        // ---- Métodos: Contains, StartsWith, EndsWith de string
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var name = node.Method.Name;

            switch (name)
            {
                case nameof(string.Contains):
                    // OData v3 (SAP SL): substringof('valor', Campo)
                    // Si tu SL es v4 puro, cambia a:  contains(Campo, 'valor')
                    _sb.Append("substringof(");
                    Visit(node.Arguments[0]);
                    _sb.Append(", ");
                    Visit(node.Object!);
                    _sb.Append(')');
                    return node;

                case nameof(string.StartsWith):
                    _sb.Append("startswith(");
                    Visit(node.Object!);
                    _sb.Append(", ");
                    Visit(node.Arguments[0]);
                    _sb.Append(')');
                    return node;

                case nameof(string.EndsWith):
                    _sb.Append("endswith(");
                    Visit(node.Object!);
                    _sb.Append(", ");
                    Visit(node.Arguments[0]);
                    _sb.Append(')');
                    return node;

                default:
                    throw new NotSupportedException($"Método '{name}' no soportado en filtros OData.");
            }
        }

        // ------------------ Helpers ------------------

        /// <summary>
        /// Convierte un valor C# en un literal OData seguro: escapa apóstrofes en strings,
        /// formatea fechas en ISO, fuerza cultura invariante en decimales (importante: con
        /// cultura española, 12.5.ToString() te da "12,5" y OData explota).
        /// </summary>
        private void AppendLiteral(object? value)
        {
            switch (value)
            {
                case null:
                    _sb.Append("null");
                    break;
                case string s:
                    _sb.Append('\'').Append(s.Replace("'", "''")).Append('\'');
                    break;
                case bool b:
                    _sb.Append(b ? "true" : "false");
                    break;
                case DateTime dt:
                    _sb.Append('\'').Append(dt.ToString("yyyy-MM-ddTHH:mm:ss")).Append('\'');
                    break;
                case DateTimeOffset dto:
                    _sb.Append('\'').Append(dto.ToString("yyyy-MM-ddTHH:mm:ssK")).Append('\'');
                    break;
                case Guid g:
                    _sb.Append('\'').Append(g.ToString()).Append('\'');
                    break;
                case decimal d:
                    _sb.Append(d.ToString(CultureInfo.InvariantCulture));
                    break;
                case double db:
                    _sb.Append(db.ToString(CultureInfo.InvariantCulture));
                    break;
                case float f:
                    _sb.Append(f.ToString(CultureInfo.InvariantCulture));
                    break;
                default:
                    _sb.Append(value.ToString());
                    break;
            }
        }

        private static string GetODataOp(ExpressionType type) => type switch
        {
            ExpressionType.Equal => "eq",
            ExpressionType.NotEqual => "ne",
            ExpressionType.GreaterThan => "gt",
            ExpressionType.GreaterThanOrEqual => "ge",
            ExpressionType.LessThan => "lt",
            ExpressionType.LessThanOrEqual => "le",
            ExpressionType.AndAlso => "and",
            ExpressionType.OrElse => "or",
            _ => throw new NotSupportedException($"Operador '{type}' no soportado.")
        };
    }
}