using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using CsvHelper;
using CsvHelper.Configuration;

namespace BTreeExample
{
    public class Book
    {
        public string ISBN { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public double Price { get; set; }
        public int Quantity { get; set; }
    }

    public class Search
    {
        public string Name { get; set; }
    }
    public class Item
    {
        public string ISBN { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public decimal? Price { get; set; }
        public int? Quantity { get; set; }
    }

    public class Operation
    {
        public string Command { get; set; }
        public string ISBN { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public decimal? Price { get; set; }
        public int? Quantity { get; set; }
    }

    public class SearchOperation
    {
        public string Name { get; set; }
    }

    public class BTreeNode
    {
        public List<Item> Items { get; private set; }
        public List<BTreeNode> Children { get; private set; }
        public bool IsLeaf { get; private set; }

        public BTreeNode(int degree, bool isLeaf)
        {
            Items = new List<Item>();
            Children = new List<BTreeNode>();
            IsLeaf = isLeaf;
        }
    }

    public class BTree
    {
        private BTreeNode Root;
        private int Degree;

        public BTree(int degree)
        {
            Degree = degree;
            Root = new BTreeNode(degree, true);
        }

        public void Insert(Item item)
        {
            var root = Root;
            if (root.Items.Count == 2 * Degree - 1)
            {
                var newRoot = new BTreeNode(Degree, false);
                newRoot.Children.Add(Root);
                SplitChild(newRoot, 0);
                Root = newRoot;
            }
            InsertNonFull(Root, item);
        }

        private void InsertNonFull(BTreeNode node, Item item)
        {
            int i = node.Items.Count - 1;
            if (node.IsLeaf)
            {
                while (i >= 0 && string.Compare(item.ISBN, node.Items[i].ISBN) < 0)
                {
                    i--;
                }
                node.Items.Insert(i + 1, item);
            }
            else
            {
                while (i >= 0 && string.Compare(item.ISBN, node.Items[i].ISBN) < 0)
                {
                    i--;
                }
                i++;
                if (node.Children[i].Items.Count == 2 * Degree - 1)
                {
                    SplitChild(node, i);
                    if (string.Compare(item.ISBN, node.Items[i].ISBN) > 0)
                    {
                        i++;
                    }
                }
                InsertNonFull(node.Children[i], item);
            }
        }

        private void SplitChild(BTreeNode parent, int index)
        {
            var fullChild = parent.Children[index];
            var newChild = new BTreeNode(Degree, fullChild.IsLeaf);

            if (fullChild.Items.Count < Degree)
            {
                throw new Exception("El nodo hijo no tiene suficientes elementos para dividir.");
            }

            parent.Children.Insert(index + 1, newChild);
            parent.Items.Insert(index, fullChild.Items[Degree - 1]);

            newChild.Items.AddRange(fullChild.Items.GetRange(Degree, Degree - 1));
            fullChild.Items.RemoveRange(Degree - 1, Degree);

            if (!fullChild.IsLeaf)
            {
                newChild.Children.AddRange(fullChild.Children.GetRange(Degree, Degree));
                fullChild.Children.RemoveRange(Degree, Degree);
            }
        }

        public void Delete(string isbn)
        {
            try
            {
                Delete(Root, isbn);
                if (Root.Items.Count == 0 && !Root.IsLeaf)
                {
                    Root = Root.Children[0];
                }
            }
            catch
            {

            }
        }

        private void Delete(BTreeNode node, string isbn)
        {
            int index = FindItemIndex(node, isbn);
            if (index < node.Items.Count && node.Items[index].ISBN == isbn)
            {
                if (node.IsLeaf)
                {
                    node.Items.RemoveAt(index);
                }
                else
                {
                    DeleteInternalNode(node, index);
                }
            }
            else
            {
                if (node.IsLeaf)
                {
                    throw new Exception($"Ítem con ISBN '{isbn}' no encontrado");
                }
                bool flag = (index == node.Items.Count);
                if (node.Children[index].Items.Count < Degree)
                {
                    Fill(node, index);
                }
                if (flag && index > node.Items.Count)
                {
                    Delete(node.Children[index - 1], isbn);
                }
                else
                {
                    Delete(node.Children[index], isbn);
                }
            }
        }

        private void DeleteInternalNode(BTreeNode node, int index)
        {
            var item = node.Items[index];
            if (node.Children[index].Items.Count >= Degree)
            {
                var pred = GetPredecessor(node.Children[index]);
                node.Items[index] = pred;
                Delete(node.Children[index], pred.ISBN);
            }
            else if (node.Children[index + 1].Items.Count >= Degree)
            {
                var succ = GetSuccessor(node.Children[index + 1]);
                node.Items[index] = succ;
                Delete(node.Children[index + 1], succ.ISBN);
            }
            else
            {
                Merge(node, index);
                Delete(node.Children[index], item.ISBN);
            }
        }

        private Item GetPredecessor(BTreeNode node)
        {
            while (!node.IsLeaf)
            {
                node = node.Children[node.Items.Count];
            }
            return node.Items[node.Items.Count - 1];
        }

        private Item GetSuccessor(BTreeNode node)
        {
            while (!node.IsLeaf)
            {
                node = node.Children[0];
            }
            return node.Items[0];
        }

        private void Merge(BTreeNode node, int index)
        {
            var child = node.Children[index];
            var sibling = node.Children[index + 1];

            if (sibling.Items.Count == 0)
            {
                throw new Exception("El nodo hermano no tiene elementos para fusionar.");
            }

            child.Items.Add(node.Items[index]);
            child.Items.AddRange(sibling.Items);

            if (!child.IsLeaf)
            {
                child.Children.AddRange(sibling.Children);
            }

            node.Items.RemoveAt(index);
            node.Children.RemoveAt(index + 1);
        }

        private void Fill(BTreeNode node, int index)
        {
            if (index != 0 && node.Children[index - 1].Items.Count >= Degree)
            {
                BorrowFromPrev(node, index);
            }
            else if (index != node.Items.Count && node.Children[index + 1].Items.Count >= Degree)
            {
                BorrowFromNext(node, index);
            }
            else
            {
                if (index != node.Items.Count)
                {
                    Merge(node, index);
                }
                else
                {
                    Merge(node, index - 1);
                }
            }
        }

        private void BorrowFromPrev(BTreeNode node, int index)
        {
            var child = node.Children[index];
            var sibling = node.Children[index - 1];

            child.Items.Insert(0, node.Items[index - 1]);

            if (!child.IsLeaf)
            {
                child.Children.Insert(0, sibling.Children[sibling.Children.Count - 1]);
                sibling.Children.RemoveAt(sibling.Children.Count - 1);
            }

            node.Items[index - 1] = sibling.Items[sibling.Items.Count - 1];
            sibling.Items.RemoveAt(sibling.Items.Count - 1);
        }

        private void BorrowFromNext(BTreeNode node, int index)
        {
            var child = node.Children[index];
            var sibling = node.Children[index + 1];

            child.Items.Add(node.Items[index]);

            if (!child.IsLeaf)
            {
                child.Children.Add(sibling.Children[0]);
                sibling.Children.RemoveAt(0);
            }

            node.Items[index] = sibling.Items[0];
            sibling.Items.RemoveAt(0);
        }

        private int FindItemIndex(BTreeNode node, string isbn)
        {
            int index = 0;
            while (index < node.Items.Count && string.Compare(isbn, node.Items[index].ISBN) > 0)
            {
                index++;
            }
            return index;
        }

        public List<Item> ToList()
        {
            var items = new List<Item>();
            ToList(Root, items);
            return items;
        }

        private void ToList(BTreeNode node, List<Item> items)
        {
            int i;
            for (i = 0; i < node.Items.Count; i++)
            {
                if (!node.IsLeaf)
                {
                    ToList(node.Children[i], items);
                }
                items.Add(node.Items[i]);
            }
            if (!node.IsLeaf)
            {
                ToList(node.Children[i], items);
            }
        }

        internal void Patch(Item updatedItem)
        {
            // Buscar el nodo donde podría encontrarse el ítem
            var node = FindNodeContainingItem(Root, updatedItem.ISBN);
            if (node != null)
            {
                // Buscar el índice del ítem dentro del nodo
                var index = FindItemIndex(node, updatedItem.ISBN);
                if (index < node.Items.Count && node.Items[index].ISBN == updatedItem.ISBN)
                {
                    var item = node.Items[index];
                    if (updatedItem.Price.HasValue)
                    {
                        item.Price = updatedItem.Price.Value;
                    }
                    if (updatedItem.Quantity.HasValue)
                    {
                        item.Quantity = updatedItem.Quantity.Value;
                    }
                }
            }
        }

        private BTreeNode FindNodeContainingItem(BTreeNode node, string isbn)
        {
            if (node == null) return null;

            int i = 0;
            while (i < node.Items.Count && string.Compare(isbn, node.Items[i].ISBN) > 0)
            {
                i++;
            }

            if (i < node.Items.Count && node.Items[i].ISBN == isbn)
            {
                return node;
            }

            if (node.IsLeaf)
            {
                return null;
            }
            else
            {
                return FindNodeContainingItem(node.Children[i], isbn);
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            string csvFilePath = "lab01_books.csv";
            string jsonFilePath = "lab01_books.json";
            string resultJsonFilePath = "result_lab01_books.json";

            var bTree = new BTree(3);

            try
            {
                // Leer y procesar el archivo CSV
                var operations = ReadAndProcessCsv(csvFilePath);

                // Convertir las operaciones a JSON y guardar en el archivo
                string json = ConvertToJson(operations);
                File.WriteAllText(jsonFilePath, json);
                Console.WriteLine("Datos convertidos a JSON y guardados en " + jsonFilePath);

                // Leer y procesar el archivo JSON
                var jsonOperations = ReadAndProcessJson(jsonFilePath);

                // Ejecutar operaciones
                foreach (var operation in jsonOperations)
                {
                    try
                    {
                        switch (operation.Command)
                        {
                            case "INSERT":
                                var item = new Item
                                {
                                    ISBN = operation.ISBN,
                                    Name = operation.Name,
                                    Author = operation.Author,
                                    Price = operation.Price ?? 0,
                                    Quantity = operation.Quantity ?? 0
                                };
                                bTree.Insert(item);
                                break;
                            case "DELETE":
                                bTree.Delete(operation.ISBN);
                                break;
                            case "PATCH":
                                var updatedItem = new Item
                                {
                                    ISBN = operation.ISBN,
                                    Price = operation.Price ?? 0,
                                    Quantity = operation.Quantity ?? 0
                                };
                                bTree.Patch(updatedItem);
                                break;
                        }
                    }
                    catch
                    {
                        // Manejo de errores sin imprimir en la consola
                    }
                }

                // Guardar el árbol B en un archivo JSON
                var items = bTree.ToList();
                string resultJson = JsonConvert.SerializeObject(items, Formatting.Indented);
                File.WriteAllText(resultJsonFilePath, resultJson);
                Console.WriteLine("Árbol B guardado en " + resultJsonFilePath);

                // Procesar el archivo lab01_search.csv
                string searchCsvFilePath = "lab01_search.csv";
                string searchJsonFilePath = "lab01_search.json";
                ProcessSearchCsvToJson(searchCsvFilePath, searchJsonFilePath);


            }
            catch
            {
                // Manejo de errores sin imprimir en la consola
            }

            Console.WriteLine();
            Console.WriteLine("Presione Enter para realizar la busqueda");
            Console.ReadLine();

            string resultFilePath = "result_lab01_books.json";
            string searchFilePath = "lab01_search.json";

            // Leer y deserializar los archivos JSON
            var books = JsonConvert.DeserializeObject<List<Book>>(File.ReadAllText(resultFilePath));
            var searches = JsonConvert.DeserializeObject<List<Search>>(File.ReadAllText(searchFilePath));

            foreach (var search in searches)
            {
                var book = books.Find(b => b.Name.Equals(search.Name, StringComparison.OrdinalIgnoreCase));
                if (book != null)
                {
                    // Imprimir la información del libro encontrado
                    Console.WriteLine($"ISBN: {book.ISBN}");
                    Console.WriteLine($"Name: {book.Name}");
                    Console.WriteLine($"Author: {book.Author}");
                    Console.WriteLine($"Price: {book.Price}");
                    Console.WriteLine($"Quantity: {book.Quantity}");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine($"No se encontró el libro con el nombre: {search.Name}");
                    Console.WriteLine();
                }
            }

            Console.ReadKey();
        }

        // Leer y procesar el archivo CSV para las operaciones de búsqueda
        private static void ProcessSearchCsvToJson(string csvFilePath, string jsonFilePath)
        {
            var searchOperations = new List<SearchOperation>();

            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                MissingFieldFound = null,
                HasHeaderRecord = false,
                BadDataFound = null
            };

            using (var reader = new StreamReader(csvFilePath))
            using (var csv = new CsvReader(reader, csvConfig))
            {
                while (csv.Read())
                {
                    string command = csv.GetField(0);
                    string jsonData = csv.GetField(1);

                    if (command == "SEARCH")
                    {
                        var searchOperation = JsonConvert.DeserializeObject<SearchOperation>(jsonData);
                        searchOperations.Add(searchOperation);
                    }
                }
            }

            // Convertir las operaciones de búsqueda a JSON y guardar en el archivo
            string json = JsonConvert.SerializeObject(searchOperations, Formatting.Indented, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            File.WriteAllText(jsonFilePath, json);
            Console.WriteLine("Datos de búsqueda convertidos a JSON y guardados en " + jsonFilePath);
        }

        // Leer y procesar el archivo CSV
        private static List<Operation> ReadAndProcessCsv(string filePath)
        {
            var operations = new List<Operation>();

            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                MissingFieldFound = null,
                HasHeaderRecord = false,
                BadDataFound = null
            };

            using (var reader = new StreamReader(filePath))
            using (var csv = new CsvReader(reader, csvConfig))
            {
                while (csv.Read())
                {
                    string command = csv.GetField(0);
                    string jsonData = csv.GetField(1);

                    if (command == "INSERT" || command == "DELETE" || command == "PATCH")
                    {
                        var item = JsonConvert.DeserializeObject<Item>(jsonData);
                        var operation = new Operation
                        {
                            Command = command,
                            ISBN = item.ISBN,
                            Name = item.Name,
                            Author = item.Author,
                            Price = command == "DELETE" ? (decimal?)null : item.Price,
                            Quantity = command == "DELETE" ? (int?)null : item.Quantity
                        };

                        operations.Add(operation);
                    }
                }
            }

            return operations;
        }

        // Convertir las operaciones a JSON
        private static string ConvertToJson(IEnumerable<Operation> operations)
        {
            return JsonConvert.SerializeObject(operations, Formatting.Indented, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
        }

        private static List<Operation> ReadAndProcessJson(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<List<Operation>>(json);
            }
            catch (FileNotFoundException)
            {
                // Manejo de errores sin imprimir en la consola
                return new List<Operation>();
            }
            catch
            {
                // Manejo de errores sin imprimir en la consola
                return new List<Operation>();
            }
        }
    }
}
