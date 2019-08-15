using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using StringToExpression.GrammerDefinitions;
using StringToExpression.LanguageDefinitions;

namespace Starship.WebCore.OData {
    public class DictionaryODataFilterLanguage : ODataFilterLanguage {
        protected override IEnumerable<GrammerDefinition> AllDefinitions() {
            var allDefinitions = base.AllDefinitions();

            //Unfortunatly expressions are picky about comparing types. Because all of our
            //properties are of type `Object` this makes things difficult. To ease some of
            //the issues we will cast an `Object` to be the same value as the other side of
            //the expression. This only works if the objects stored in the dictionary are
            //the same types as the constants in the expression, i.e. int, decimal,
            return allDefinitions.Select(def => {
                var binary = def as BinaryOperatorDefinition;
                if (binary == null)
                    return def;

                return new BinaryOperatorDefinition(binary.Name,
                    binary.Regex,
                    binary.OrderOfPrecedence.Value,
                    (left, right) => {
                        if (left.Type == typeof(object) && right.Type.IsValueType)
                            left = Expression.Convert(left, right.Type);
                        else if (left.Type.IsValueType && right.Type == typeof(object))
                            right = Expression.Convert(right, left.Type);
                        return binary.ExpressionBuilder(new[] {left, right});
                    });
            });
        }

        protected override IEnumerable<GrammerDefinition> PropertyDefinitions() {
            return new[] {
                //Properties
                new OperandDefinition(
                    name: "PROPERTY_PATH",
                    regex: @"(?<![0-9])([A-Za-z_][A-Za-z0-9_]*/?)+",
                    expressionBuilder: (value, parameters) => {
                        var row = (Expression) parameters[0];

                        //we will retrieve the value of the property by reading the indexer property
                        var indexProperty = row.Type.GetDefaultMembers().OfType<PropertyInfo>().Single();
                        return Expression.Call(row, indexProperty.GetMethod, Expression.Constant(value, typeof(string)));
                    }),
            };
        }

        /*protected override IEnumerable<GrammerDefinition> LogicalOperatorDefinitions() {
            var logicalOperations = base.LogicalOperatorDefinitions();
            var newEquals = new BinaryOperatorDefinition(
                name: "EQ",
                regex: @"eq",
                orderOfPrecedence: 11,
                expressionBuilder: (left, right) => {
                    if (left.Type == typeof(string) || right.Type == typeof(string)) {
                        var stringEquals = typeof(string).GetMethod(
                            "Equals",
                            new Type[] {typeof(string), typeof(string), typeof(StringComparison)});

                        return Expression.Call(stringEquals,
                            Expression.Convert(left, typeof(string)),
                            Expression.Convert(right, typeof(string)),
                            Expression.Constant(StringComparison.OrdinalIgnoreCase));
                    }

                    return Expression.Equal(left, right);
                });

            return logicalOperations.Where(x => x.Name != "EQ").Concat(new[] {newEquals});
        }*/
    }
}