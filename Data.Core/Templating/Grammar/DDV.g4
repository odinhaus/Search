grammar DDV;


/*
 * Parser Rules
 */

booleanOperator
	: BooleanOperator
	;

logicalOperator
	: LogicalOperator
	;

assignmentOperator
	:	AssignmentOperator
	;

incrementOperator
	:	IncrementOperator
	;

arithmeticOperator
	:	ArithmeticOperator
	;

objectExpression
	:	BraceOpen (scopedVariable | namedElement) Colon parameter (Comma (scopedVariable | namedElement) Colon parameter)* BraceClose
	;

variable
	:	At namedElement
	;

lambdaVariable
	:	decimalLiteral Dollar 
	;

decimalLiteral
	:	DecimalLiteral
	;

variableDeclaration
	:	'var' variable
	;

scopedVariable
	:	At At namedElement
	;

stringLiteral
	:	StringLiteral
	;

numberLiteral
	:   floatLiteral | integerLiteral
	;

literal
    :   booleanLiteral
	|	numberLiteral
	|   stringLiteral
    |   nullLiteral
    ;

nullLiteral
	: Null
	;


integerLiteral
    :   HexLiteral
    |   DecimalLiteral
    ;

booleanLiteral
    : Boolean
	;

floatLiteral
	:	FLOAT
	;

namedElement
	:	NamedElement
	;

dot
	:	Dot
	;

end
	:	End
	;

semiColon
	:	StatementEnd
	;

endIterator
	:	'end;'
	;

property
	:	(dot namedElement ('[' (literal | propertyAccess | methodAccess | arithmeticExpression | function) ']')? )+
	;

propertyAccess
	:	(variable | lambdaVariable | function | scopedVariable) property?
	|	(variable | lambdaVariable | function | scopedVariable) ('[' (literal | propertyAccess | methodAccess | arithmeticExpression | function) ']') property?
	;

arrayOfLiterals
	:	BracketOpen ((literal | propertyAccess | methodAccess | variable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | objectExpression) 
					(Comma (literal | propertyAccess | methodAccess | variable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | objectExpression))*)?
		BracketClose
	;

function
	:	(variable | lambdaVariable | namedElement) OpenParen CloseParen
	|	(variable | lambdaVariable | namedElement) OpenParen parameter (Comma parameter)* CloseParen	
	;

parameter
	:	inlineStatement | arithmeticExpression | binaryExpression | function | ternaryFunction | objectExpression | functionDefinition | literal | arrayOfLiterals 
	;

arithmeticExpression
	:	OpenParen arithmeticExpression CloseParen
	|	(numberLiteral | propertyAccess | methodAccess | function | variable | lambdaVariable | scopedVariable )
	|	(numberLiteral | propertyAccess | methodAccess | function | variable | lambdaVariable | scopedVariable ) arithmeticOperator (numberLiteral | propertyAccess | function | variable | lambdaVariable | scopedVariable)
	|	arithmeticExpression (arithmeticOperator arithmeticExpression)+
	;

forEachIterator
	:	(variable | variableDeclaration) In (propertyAccess | variable | arrayOfLiterals | methodAccess)
	;

forIterator
	:	assignment StatementEnd binaryExpression StatementEnd assignment
	;

forLoopExpression
	:	ForEach OpenParen forEachIterator CloseParen BraceOpen (assignmentExpression | functionExpression | memberExpression | COMMENT)* BraceClose
	|   For OpenParen forIterator CloseParen BraceOpen (assignmentExpression | functionExpression | memberExpression | COMMENT)* BraceClose
	;

ifElseExpression
	:	Else If OpenParen logicalExpression CloseParen BraceOpen (assignmentExpression | functionExpression | memberExpression | COMMENT)+ BraceClose
	;

elseExpression
	:	Else BraceOpen (assignmentExpression | functionExpression | memberExpression | COMMENT)+ BraceClose
	;

ifExpression
	:	If OpenParen logicalExpression CloseParen BraceOpen (assignmentExpression | functionExpression | memberExpression | COMMENT)+ BraceClose ifElseExpression* elseExpression?
	;

method
	:	((dot function) | property)* (dot function)
	;

methodAccess
	:	variable method?
	|	lambdaVariable method?
	|   function method?
	|	scopedVariable method?
	;

binaryExpression
	:	(function | literal | propertyAccess | methodAccess | arithmeticExpression | ternaryFunction | variable  | lambdaVariable | scopedVariable) booleanOperator (function | literal | propertyAccess | methodAccess | arithmeticExpression | ternaryFunction | variable  | lambdaVariable | scopedVariable)
	|	booleanLiteral
	|	propertyAccess /* in the event the property is of type boolean, the equality to TRUE is implied */
	|	function
	;

logicalExpression
	:	OpenParen logicalExpression CloseParen
	|	(booleanLiteral | binaryExpression)
	|	(booleanLiteral | binaryExpression) logicalOperator (booleanLiteral | binaryExpression)
	|	logicalExpression logicalOperator logicalExpression
	;	

returnExpression
	:	Return parameter? StatementEnd
	;

typedArgument
	:	namedElement unTypedArgument
	;

unTypedArgument
	:	lambdaVariable
	;

argument
	:	typedArgument | unTypedArgument
	;

functionDefinition
	:	OpenParen (argument (Comma argument)*)? CloseParen 
			LambdaAssign
			BraceOpen
				(assignmentExpression | functionExpression | memberExpression | COMMENT)* returnExpression 
			BraceClose
	;

functionExpression
	:	function semiColon
	|	forLoopExpression 
	|	ifExpression
	;

memberExpression
	:	(propertyAccess | methodAccess) semiColon
	;

assignment
	:	(variable | variableDeclaration | propertyAccess) 
		assignmentOperator 
		(literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | logicalExpression | ternaryFunction | objectExpression | functionDefinition)
	|   variable incrementOperator
	;

ternaryFunction
	:	(function | literal | propertyAccess | methodAccess | arithmeticExpression | variable  | lambdaVariable) booleanOperator (function | literal | propertyAccess | methodAccess | arithmeticExpression | ternaryFunction | variable | lambdaVariable) 
				'?' (variable | lambdaVariable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable) 
				':' (variable | lambdaVariable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable)
	|	function 
				'?' (variable | lambdaVariable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable) 
				':' (variable | lambdaVariable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable)
	|	propertyAccess 
				'?' (variable | lambdaVariable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable) 
				':' (variable | lambdaVariable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable)
	|	booleanLiteral 
				'?' (variable | lambdaVariable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable) 
				':' (variable | lambdaVariable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable)
	|	ternaryFunction 
				'?' (variable | lambdaVariable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable) 
				':' (variable | lambdaVariable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable)
	;

assignmentExpression
	:	assignment semiColon
	;

codeStatement
	:	('<ddv>')? ('<![CDATA[')? (assignmentExpression | functionExpression | memberExpression | COMMENT)* (']]>')? ('</ddv>')?
	;

inlineStatement
	:	('[[') (variable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable | arithmeticExpression) (']]')
	|	(variable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable | arithmeticExpression)
	;

//text
//	:	Text
//	;


/*
 * Lexer Rules
 */

 BooleanOperator
	:	'=='
	|	'>'
	|	'>='
	|	'<'
	|	'<='
	|	'!='
	;

 LogicalOperator
	:	'&&'
	|	'||'
	|	'and'
	|	'or'
	;

 ArithmeticOperator
	:	'+'
	|	'-'
	|	'/'
	|	'*'
	|	'%'
	|	'^'
	|	'|'
	|	'&'
	;

 AssignmentOperator
	:	'='
	|	'+='
	|	'-='
	|	'*='
	|	'\\='
	;

IncrementOperator
	:	'++'
	|	'--'
	;

If
	:	'if'
	;

Else
	:	'else'
	;

 OpenParen
	:	'('
	;

CloseParen
	:	')'
	;

Null
	:	'null'
	|	'NULL'
	;

 At
	:	'@'
	;

Pound
	:	'#'
	;

Dollar
	:	'$'
	;

Minus
	:	'-'
	;

Boolean
	:   'true'
    |   'false'
    ;

BracketOpen
	:	'['
	;

BracketClose
	:	']'
	;

BraceOpen
	:	'{'
	;

BraceClose
	:	'}'
	;

Comma
	:	','
	;

Colon
	:	':'
	;

ForEach
	:	'foreach'
	;

For
	:	'for'
	;

In
	:	'in'
	;

Return
	:	'return'
	;

COMMENT
    : '/*' .*? '*/' NewLine*
    ;

WS
	:	(' ' | '\r' | '\n' | '\t') -> channel(HIDDEN)
	;

StringLiteral
    :	'"' ( EscapeSequence | ~('\\'|'"') )* '"'
	|	'\'' ( EscapeSequence | ~('\\'|'\'') )* '\''
    ;

FLOAT
    :   (Minus)? ('0'..'9')+ Dot ('0'..'9')* EXPONENT?
    |   (Minus)? Dot ('0'..'9')+ EXPONENT?
    |   (Minus)? ('0'..'9')+ EXPONENT
    ;

DecimalLiteral : (Minus)? ('0' | '1'..'9' '0'..'9'*);

fragment
HEX_DIGIT : ('0'..'9'|'a'..'f'|'A'..'F') ;

HexLiteral : (Minus)? '0' ('x'|'X') HEX_DIGIT ;

NamedElement  
	:	('a'..'z'|'A'..'Z'|'_') ('a'..'z'|'A'..'Z'|'0'..'9'|'_')*
    ;

Dot
	:	'.'
	;

LambdaAssign
	:	'=>'
	;

End
	:	'end'
	;

fragment
EXPONENT : ('e'|'E') ('+'|'-')? ('0'..'9')+ ;


fragment
EscapeSequence
    :   '\\' ('b'|'t'|'n'|'f'|'r'|'\"'|'\\'|'\'')
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

StatementEnd
	:	SemiColon NewLine*
	;

fragment SemiColon : ';';
fragment NewLine   : '\r' '\n' | '\n' | '\r';



