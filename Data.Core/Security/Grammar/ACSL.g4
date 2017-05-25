grammar ACSL;

/*
 * Parser Rules
 */

booleanOperator
	:	'=='
	|	'='
	|	'>'
	|	'>='
	|	'<'
	|	'<='
	|	'!='
	;

logicalOperator
	:	'&&'
	|	'||'
	|	'and'
	|	'or'
	;

arithmeticOperator
	:	'+'
	|	'-'
	|	'/'
	|	'*'
	;

arithmeticExpression
	:	(numberLiteral | propertyAccess) (arithmeticOperator (numberLiteral | propertyAccess))*
	|	'(' arithmeticExpression ')'
	;

validModel
	:	'@' namedElement
	;

stringLiteral
	:	StringLiteral
	;

numberLiteral
	:   floatLiteral | integerLiteral
	;

literal
    :   integerLiteral
    |   booleanLiteral
	|	floatLiteral
	|   stringLiteral
    |   nullLiteral
    ;

nullLiteral
	:	'null'
	|	'NULL'
	;


integerLiteral
    :   ('-')? HexLiteral
    |   ('-')? DecimalLiteral
    ;

booleanLiteral
    :   'true'
    |   'false'
    ;

floatLiteral
	:	('-')? FLOAT
	;

namedElement
	:	NamedElement
	;

dot
	: Dot
	;

property
	:	(dot namedElement)+
	;

propertyAccess
	:	validModel property?
	;

arrayOfLiterals
	:	'[' (literal | validModel) (',' (literal | validModel))* ']'
	;

/*
isOwnedBy
	:	'IsOwnedBy(' propertyAccess ',' (stringLiteral | propertyAccess) ')'
	;

isInContainer
	:	'IsInContainer(' propertyAccess ',' (stringLiteral | propertyAccess) ')'
	;

isInOrgUnit
	:	'IsInOrgUnit(' propertyAccess ',' (stringLiteral | propertyAccess) ')'
	;

isInRole
	:	'IsInRole(' propertyAccess ',' (stringLiteral | propertyAccess) ')'
	;

contains
	:	'Contains(' (stringLiteral | propertyAccess) ',' (stringLiteral | propertyAccess) ')'
	|	'Contains(' (arrayOfLiterals | propertyAccess) ',' (literal | arrayOfLiterals | propertyAccess) ')'
	;

startsWith
	:	'StartsWith(' (stringLiteral | propertyAccess) ',' (stringLiteral | propertyAccess) ')'
	;

endsWith
	:	'EndsWith(' (stringLiteral | propertyAccess) ',' (stringLiteral | propertyAccess) ')'
	;

function
	:	isInContainer | isInOrgUnit | isInRole | isOwnedBy | contains | startsWith | endsWith
	;
*/

function
	:	namedElement '()'
	|	namedElement'(' (literal | propertyAccess | validModel | arrayOfLiterals | arithmeticExpression | binaryExpression | function) (',' (literal | propertyAccess | validModel | arrayOfLiterals | arithmeticExpression | binaryExpression | function))* ')'
	;

binaryExpression
	:	(function | literal | propertyAccess | arithmeticExpression) booleanOperator (function | literal | propertyAccess | arithmeticExpression)
	|	booleanLiteral
	|	propertyAccess /* in the event the property is of type boolean, the equality to TRUE is implied */
	|	function
	;

logicalExpression
	:	'(' logicalExpression ')'
	|	(booleanLiteral | binaryExpression)
	|	(booleanLiteral | binaryExpression) logicalOperator (booleanLiteral | binaryExpression)
	|	logicalExpression logicalOperator logicalExpression
	;	

/*
 * Lexer Rules
 */

WS
	:	' ' -> channel(HIDDEN)
	;

StringLiteral
    :	'"' ( EscapeSequence | ~('\\'|'"') )* '"'
	|	'\'' ( EscapeSequence | ~('\\'|'\'') )* '\''
    ;

FLOAT
    :   ('0'..'9')+ '.' ('0'..'9')* EXPONENT?
    |   '.' ('0'..'9')+ EXPONENT?
    |   ('0'..'9')+ EXPONENT
    ;

DecimalLiteral : ('0' | '1'..'9' '0'..'9'*);

fragment
HEX_DIGIT : ('0'..'9'|'a'..'f'|'A'..'F') ;

HexLiteral : '0' ('x'|'X') HEX_DIGIT ;

NamedElement  
	:	('a'..'z'|'A'..'Z'|'_') ('a'..'z'|'A'..'Z'|'0'..'9'|'_')*
    ;

Dot
	: '.'
	;

fragment
EXPONENT : ('e'|'E') ('+'|'-')? ('0'..'9')+ ;

fragment
ESC_SEQ
    :   '\\' ('b'|'t'|'n'|'f'|'r'|'\"'|'\''|'\\')
    |   UNICODE_ESC
    |   OCTAL_ESC
    ;

fragment
OCTAL_ESC
    :   '\\' ('0'..'3') ('0'..'7') ('0'..'7')
    |   '\\' ('0'..'7') ('0'..'7')
    |   '\\' ('0'..'7')
    ;

fragment
UNICODE_ESC
    :   '\\' 'u' HEX_DIGIT HEX_DIGIT HEX_DIGIT HEX_DIGIT
    ;

fragment
EscapeSequence
    :   '\\' ('b'|'t'|'n'|'f'|'r'|'\"'|'\\')
    |   UnicodeEscape
    |   OctalEscape
    ;

fragment
OctalEscape
    :   '\\' ('0'..'3') ('0'..'7') ('0'..'7')
    |   '\\' ('0'..'7') ('0'..'7')
    |   '\\' ('0'..'7')
    ;

fragment
UnicodeEscape
    :   '\\' 'u' HEX_DIGIT HEX_DIGIT HEX_DIGIT HEX_DIGIT
    ;
