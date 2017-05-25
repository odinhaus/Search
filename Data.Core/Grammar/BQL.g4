grammar BQL;

queryExpression
	: aggregateExpression sortExpression? limitExpression? projectorExpression? 
	;

operator
	:	'='
	|	'>'
	|	'>='
	|	'<'
	|	'<='
	|	'!='
	|	'contains'
	|	'startswith'
	|	'is'
	|	'is not'
	;

specialFunction
	:	'@@current_username'
	|	current_Date
	|	current_DateTime
	;

current_Date
	:	'@@current_date'
	;

current_DateTime
	:	'@@current_datetime'
	;

aggregator
	:	'UNION'
	|	'INTERSECT'
	|	'EXCLUDE'
	;

aggregateExpression
	:	'(' aggregateExpression ')'
	|	predicateExpression (aggregator predicateExpression)*
	;

projectorExpression
	:	projector projectorType
	;

projector
	:	'RETURNS'
	;

projectorType
	:	'NODES'
	|	'PATHS'
	;

sort
	:	'SORT'
	;

sortOrder
	:	'ASC'
	|	'DESC'
	;

sortExpression
	:	sort namedElement sortOrder? (',' namedElement sortOrder?)*
	;

rank
	:	'RANK'
	;

rankExpression
	:	rank numberLiteral
	;

limit
	:	'LIMIT'
	;

limitExpression
	:	limit (integerLiteral ',')? integerLiteral
	;

bool
	:	'&'
	|	'|'
	|	'and'
	|	'or'
	;

expression
	:	binaryExpression
	|	booleanExpression
	;

booleanExpression
	:	'(' booleanExpression ')'
	|	binaryExpression bool binaryExpression
	|	booleanExpression bool binaryExpression
	|	binaryExpression bool booleanExpression
	|	booleanExpression bool booleanExpression
	;

binaryExpression
	:	primaryBinaryPropertyOperand secondaryBinaryPropertyOperand
	;

primaryBinaryPropertyOperand
	:	propertyExpression operator
	;

secondaryBinaryPropertyOperand
	:	literal 
	|	propertyExpression
	|	specialFunction
	|	function
	;

function
	:	dateFunction
	;

dateFunction
	:	date_Timestamp
	|	date_ISO8601
	|	date_DayOfWeek
	|	date_Year
	|	date_Month
	|	date_Day
	|	date_Hour
	|	date_Minute
	|	date_Second
	|	date_Millisecond
	|	date_DayOfYear
	|	date_Add
	|	date_Subtract
	|	date_Diff
	;

date_Timestamp
	:	'DATE_TIMESTAMP(' date ')'
	|	'DATE_TIMESTAMP(' integerLiteral ',' integerLiteral ',' integerLiteral (',' integerLiteral (',' integerLiteral (',' integerLiteral (',' integerLiteral )?)?)?)? ')'
	;

date_ISO8601
	:	'DATE_ISO8601(' date ')'
	|	'DATE_ISO8601(' integerLiteral ',' integerLiteral ',' integerLiteral (',' integerLiteral (',' integerLiteral (',' integerLiteral (',' integerLiteral )?)?)?)? ')'
	;

date_DayOfWeek
	:	'DATE_DAYOFWEEK(' date ')'
	;

date_Year
	:	'DATE_YEAR(' date ')'
	;

date_Month
	:	'DATE_MONTH(' date ')'
	;

date_Day
	:	'DATE_DAY(' date ')'
	;

date_Hour
	:	'DATE_HOUR(' date ')'
	;

date_Minute
	:	'DATE_MINUTE(' date ')'
	;

date_Second
	:	'DATE_SECOND(' date ')'
	;

date_Millisecond
	:	'DATE_MILLISECOND(' date ')'
	;

date_DayOfYear
	:	'DATE_DAYOFYEAR(' date ')'
	;

date_Add
	:	'DATE_ADD(' date ',' integerLiteral ',' date_Unit ')'
	;

date_Subtract
	:	'DATE_SUBTRACT(' date ',' integerLiteral ',' date_Unit ')'
	;

date_Diff
	:	'DATE_DIFF(' date ',' date ',' date_Unit ')'
	;

date_Unit
	:	'\'y\''
	|	'\'m\''
	|	'\'d\''
	|	'\'h\''
	|	'\'i\''
	|	'\'s\''
	|	'\'f\''
	|	'\'year\''
	|	'\'month\''
	|	'\'day\''
	|	'\'hour\''
	|	'\'minute\''
	|	'\'second\''
	|	'\'millisecond\''
	;

date
	:	stringLiteral
	|	integerLiteral
	|	current_Date
	|	current_DateTime
	|	date_Timestamp
	|	date_ISO8601
	|	date_Add
	|	date_Subtract
	;

propertyExpression
	:	(namedElement | literal) ('.' (namedElement | literal) )*
	;

qualifiedElement
	:	namedElement ('.' namedElement)*
	;

predicateExpression
	:	vertexAccessor ((edgeAccessor | vertexIn | vertexOut | notVertexIn | notVertexOut | optionalVertexIn | optionalVertexOut) vertexAccessor)*
	;

edgeAccessor
	:	edgeIn qualifiedElement filterExpression? edgeIn
	|	edgeOut qualifiedElement filterExpression? edgeOut
	|	notEdgeIn qualifiedElement filterExpression? notEdgeIn
	|	notEdgeOut qualifiedElement filterExpression? notEdgeOut
	|	optionalEdgeIn qualifiedElement filterExpression? optionalEdgeIn
	|	optionalEdgeOut qualifiedElement filterExpression? optionalEdgeOut
	;

vertexAccessor
	:	qualifiedElement filterExpression?
	;

filterExpression
	:	'{' expression (options {greedy=false;} : '}')
	;

numberLiteral
	:	floatLiteral | integerLiteral
	;

literal
    :   integerLiteral
    |   booleanLiteral
	|	floatLiteral
	|   stringLiteral
	|	paramLiteral
    |   'null'
    ;

paramLiteral
	:	'$' integerLiteral
	;

integerLiteral
    :   HexLiteral
    |   DecimalLiteral
    ;

booleanLiteral
    :   'true'
    |   'false'
    ;

stringLiteral
	:	StringLiteral
	;

floatLiteral
	: FLOAT
	;

namedElement
	:	NamedElement
	;
	
elementInstance
	:	'#' DecimalLiteral ':' DecimalLiteral
	;

edgeIn
	:	'<-'
	;

edgeOut
	:	'->'
	;

notEdgeIn
	:	'<~'
	;

notEdgeOut
	:	'~>'
	;

optionalEdgeIn
	:	'<+'
	;

optionalEdgeOut
	:	'+>'
	;

vertexIn
	:	'<--'
	;

vertexOut
	:	'-->'
	;

notVertexIn
	:	'<~~'
	;

notVertexOut
	:	'~~>'
	;

optionalVertexIn
	:	'<++'
	;

optionalVertexOut
	:	'++>'
	;

StringLiteral
    :	'"' ( EscapeSequence | ~('\\'|'"') )* '"'
	|	'\'' ( EscapeSequence | ~('\\'|'\'') )* '\''
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

NamedElement  
	:	('a'..'z'|'A'..'Z'|'_') ('a'..'z'|'A'..'Z'|'0'..'9'|'_')*
	|	Wildcard
    ;

Wildcard
	:	'*'
	;

FLOAT
    :   ('0'..'9')+ '.' ('0'..'9')* EXPONENT?
    |   '.' ('0'..'9')+ EXPONENT?
    |   ('0'..'9')+ EXPONENT
    ;

COMMENT
    :   '/*' .*? '*/'    -> channel(HIDDEN) // match anything between /* and */
    ;

LINE_COMMENT
    : '//' ~[\r\n]* '\r'? '\n' -> channel(HIDDEN)
    ;

WS  :   [ \r\t\u000C\n]+ -> channel(HIDDEN)
    ;

STRING
    :  '"' ( ESC_SEQ | ~('\\'|'"') )* '"'
    ;

CHAR:  '\'' ( ESC_SEQ | ~('\''|'\\') ) '\''
    ;
	
DecimalLiteral : ('0' | '1'..'9' '0'..'9'*);

fragment
EXPONENT : ('e'|'E') ('+'|'-')? ('0'..'9')+ ;

fragment
HEX_DIGIT : ('0'..'9'|'a'..'f'|'A'..'F') ;

HexLiteral : '0' ('x'|'X') HEX_DIGIT ;


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
