-- Sample seed script
CREATE TABLE IF NOT EXISTS Items (
    Id INT PRIMARY KEY,
    Name NVARCHAR(100),
    Category NVARCHAR(50),
    Value DECIMAL(10,2)
);

INSERT INTO Items (Id, Name, Category, Value) VALUES
(1, 'Example Item A', 'Category 1', 100.00),
(2, 'Example Item B', 'Category 2', 200.00),
(3, 'Example Item C', 'Category 1', 150.00);
