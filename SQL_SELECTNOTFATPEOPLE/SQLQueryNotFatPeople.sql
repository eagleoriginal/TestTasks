use AdventureWorks2014;

IF OBJECT_ID('tempDB..#CustomerPeople_q', 'U') IS NOT NULL 
	Drop table #CustomerPeople_q;

IF OBJECT_ID('tempDB..#CustomerPurchase_q', 'U') IS NOT NULL 
	Drop table #CustomerPurchase_q;

CREATE TABLE #CustomerPeople_q(
CustomerId [int] PRIMARY KEY IDENTITY(1,1) NOT NULL,
RegistrationDateTime [datetime] NOT NULL
)

CREATE TABLE #CustomerPurchase_q(
CustomerId [int] References #CustomerPeople_q(CustomerId),
PurchaiseDatetime [datetime] NOT NULL,
ProductName nvarchar(100) NOT NULL
)
GO

insert into #CustomerPeople_q 
select DATEADD(DD, -100, GETDATE())
GO 100

insert into #CustomerPurchase_q (CustomerId, PurchaiseDatetime, ProductName)
values (100*RAND(), DATEADD(DD, -ROUND(60*RAND(), 0, 0), GETDATE()), 'Moloko')
GO 30

insert into #CustomerPurchase_q (CustomerId, PurchaiseDatetime, ProductName)
VALUES (100*RAND(), GETDATE(), 'Smetana'),
(100*RAND(), DATEADD(MONTH, -1, GETDATE() ), 'Smetana')
GO 10

select * from #CustomerPurchase_q order by CustomerId

select distinct p1.CustomerId from (select distinct * from #CustomerPurchase_q where ProductName like 'moloko') as p1 
where p1.CustomerId not in 
(select distinct CustomerId from 
#CustomerPurchase_q
where 
PurchaiseDatetime >= DATEADD(MONTH, DATEDIFF(MONTH, 0, GETDATE()), 0)
AND ProductName like 'smetana') 
order by CustomerId

Drop table #CustomerPeople_q;
Drop table #CustomerPurchase_q;
