using System;
using System.Collections.Generic;
using System.Linq;

namespace lab5v7
{
    // Власний виняток: повернення раніше дати видачі
    public class InvalidReturnDateException : Exception
    {
        public InvalidReturnDateException(string message) : base(message)
        {
        }
    }

    // Сутність 1: Запис про видачу книги (Loan)
    public class Loan
    {
        public int Id { get; private set; }
        public string BookTitle { get; private set; }
        public string ReaderName { get; private set; }
        public DateTime LoanDate { get; private set; }
        public DateTime DueDate { get; private set; }          // кінцевий строк
        public DateTime? ReturnDate { get; private set; }      // фактичне повернення (може бути null)
        public decimal DailyRate { get; private set; }         // ставка штрафу за 1 день

        public bool IsReturned
        {
            get { return ReturnDate.HasValue; }
        }

        public Loan(int id,
                    string bookTitle,
                    string readerName,
                    DateTime loanDate,
                    DateTime dueDate,
                    decimal dailyRate)
        {
            if (string.IsNullOrWhiteSpace(bookTitle))
                throw new ArgumentException("Назва книги не може бути порожньою.", "bookTitle");

            if (string.IsNullOrWhiteSpace(readerName))
                throw new ArgumentException("Ім'я читача не може бути порожнім.", "readerName");

            if (dueDate < loanDate)
                throw new ArgumentException("Дата здачі не може бути раніше дати видачі.", "dueDate");

            Id = id;
            BookTitle = bookTitle;
            ReaderName = readerName;
            LoanDate = loanDate;
            DueDate = dueDate;
            DailyRate = dailyRate;
        }

        // Встановити дату повернення
        public void SetReturnDate(DateTime returnDate)
        {
            if (returnDate < LoanDate)
            {
                throw new InvalidReturnDateException(
                    string.Format("Невірна дата повернення ({0:d}) для книги \"{1}\" – вона раніша за дату видачі ({2:d}).",
                                  returnDate, BookTitle, LoanDate));
            }

            ReturnDate = returnDate;
        }

        // Кількість днів прострочки
        public int GetOverdueDays(DateTime? asOf)
        {
            DateTime reference;
            if (ReturnDate.HasValue)
                reference = ReturnDate.Value;
            else if (asOf.HasValue)
                reference = asOf.Value;
            else
                reference = DateTime.Today;

            if (reference <= DueDate)
                return 0;

            return (reference.Date - DueDate.Date).Days;
        }

        // Розрахунок штрафу
        public decimal GetFine(DateTime? asOf)
        {
            int overdueDays = GetOverdueDays(asOf);
            return overdueDays * DailyRate;
        }

        public override string ToString()
        {
            string status = IsReturned
                ? string.Format("Повернена {0:dd.MM.yyyy}", ReturnDate)
                : "Ще на руках";

            return string.Format("#{0}: \"{1}\" для {2}, видана {3:dd.MM.yyyy}, до {4:dd.MM.yyyy}, {5}",
                                 Id, BookTitle, ReaderName, LoanDate, DueDate, status);
        }
    }

    // Узагальнений репозиторій (Generics) – Repository<T>
    public class Repository<T>
    {
        private readonly List<T> _items = new List<T>();

        public void Add(T item)
        {
            _items.Add(item);
        }

        public bool Remove(T item)
        {
            return _items.Remove(item);
        }

        public IEnumerable<T> GetAll()
        {
            return _items;
        }

        public IEnumerable<T> Where(Func<T, bool> predicate)
        {
            return _items.Where(predicate);
        }

        public T Find(Func<T, bool> predicate)
        {
            // Повертаємо default(T), якщо не знайдено (як у FirstOrDefault)
            return _items.FirstOrDefault(predicate);
        }
    }

    // Сутність 2: Бібліотека, яка МІСТИТЬ Repository<Loan> (композиція)
    public class Library
    {
        public string Name { get; private set; }
        private readonly Repository<Loan> _loanRepository = new Repository<Loan>();

        public Library(string name)
        {
            Name = name;
        }

        public void AddLoan(Loan loan)
        {
            _loanRepository.Add(loan);
        }

        public IEnumerable<Loan> GetAllLoans()
        {
            return _loanRepository.GetAll();
        }

        // Фільтр по читачу
        public IEnumerable<Loan> GetLoansByReader(string readerName)
        {
            return _loanRepository.Where(
                l => string.Equals(l.ReaderName, readerName, StringComparison.OrdinalIgnoreCase));
        }

        // Фільтр по книзі
        public IEnumerable<Loan> GetLoansByBook(string bookTitle)
        {
            return _loanRepository.Where(
                l => string.Equals(l.BookTitle, bookTitle, StringComparison.OrdinalIgnoreCase));
        }

        // Усі прострочені на певну дату
        public IEnumerable<Loan> GetOverdueLoans(DateTime? asOf)
        {
            return _loanRepository.Where(l => l.GetOverdueDays(asOf) > 0);
        }

        // Сумарний штраф
        public decimal GetTotalFine(DateTime? asOf)
        {
            decimal sum = 0;
            foreach (Loan loan in _loanRepository.GetAll())
            {
                sum += loan.GetFine(asOf);
            }
            return sum;
        }

        // Середній штраф серед прострочених
        public decimal GetAverageFineForOverdue(DateTime? asOf)
        {
            List<decimal> fines = new List<decimal>();
            foreach (Loan loan in _loanRepository.GetAll())
            {
                decimal fine = loan.GetFine(asOf);
                if (fine > 0)
                    fines.Add(fine);
            }

            if (fines.Count == 0)
                return 0;

            decimal sum = 0;
            foreach (decimal f in fines)
                sum += f;

            return sum / fines.Count;
        }
    }

    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Library library = new Library("Міська бібліотека");

            // Демонстрація створення даних + обробка винятків
            try
            {
                // Коректні видачі
                library.AddLoan(new Loan(
                    1,
                    "Гаррі Поттер",
                    "Андрій",
                    new DateTime(2025, 11, 1),
                    new DateTime(2025, 11, 10),
                    5m));

                library.AddLoan(new Loan(
                    2,
                    "1984",
                    "Марія",
                    new DateTime(2025, 11, 3),
                    new DateTime(2025, 11, 8),
                    10m));

                library.AddLoan(new Loan(
                    3,
                    "Мандрівний замок",
                    "Андрій",
                    new DateTime(2025, 11, 5),
                    new DateTime(2025, 11, 12),
                    7m));

                // Приклад помилки з нашою InvalidReturnDateException
                Loan badLoan = new Loan(
                    4,
                    "Дюна",
                    "Олег",
                    new DateTime(2025, 11, 10),
                    new DateTime(2025, 11, 20),
                    8m);

                // Неправильна дата повернення – раніше, ніж дата видачі
                badLoan.SetReturnDate(new DateTime(2025, 11, 5));

                library.AddLoan(badLoan);
            }
            catch (InvalidReturnDateException ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("=== Опрацьована помилка ===");
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Невідома помилка: " + ex.Message);
                Console.ResetColor();
            }

            // Для звіту припустимо, що сьогодні 25.11.2025
            DateTime reportDate = new DateTime(2025, 11, 25);

            // Проставимо дати повернення деяким книгам
            foreach (Loan loan in library.GetAllLoans())
            {
                if (loan.Id == 1)
                    loan.SetReturnDate(new DateTime(2025, 11, 9));   // вчасно
                if (loan.Id == 2)
                    loan.SetReturnDate(new DateTime(2025, 11, 15));  // з прострочкою
                // loan.Id == 3 залишаємо не поверненою
            }

            Console.WriteLine("=== Усі видачі ===");
            foreach (Loan loan in library.GetAllLoans())
            {
                Console.WriteLine(loan);
                int overdue = loan.GetOverdueDays(reportDate);
                decimal fine = loan.GetFine(reportDate);
                Console.WriteLine(string.Format("   Прострочка: {0} дн., штраф: {1} грн", overdue, fine));
            }

            Console.WriteLine();
            Console.WriteLine("=== Прострочені видачі на {0:d} ===", reportDate);
            foreach (Loan loan in library.GetOverdueLoans(reportDate))
            {
                Console.WriteLine(string.Format("{0} для {1}: штраф {2} грн",
                                                loan.BookTitle,
                                                loan.ReaderName,
                                                loan.GetFine(reportDate)));
            }

            Console.WriteLine();
            Console.WriteLine("Загальний штраф по бібліотеці: {0} грн",
                              library.GetTotalFine(reportDate));
            Console.WriteLine("Середній штраф серед прострочених: {0:F2} грн",
                              library.GetAverageFineForOverdue(reportDate));

            Console.WriteLine();
            Console.WriteLine("=== Пошук по читачу (Андрій) ===");
            foreach (Loan loan in library.GetLoansByReader("Андрій"))
            {
                Console.WriteLine(loan);
            }

            Console.WriteLine();
            Console.WriteLine("=== Пошук по книзі (\"1984\") ===");
            foreach (Loan loan in library.GetLoansByBook("1984"))
            {
                Console.WriteLine(loan);
            }

            Console.WriteLine();
            Console.WriteLine("Натисніть будь-яку клавішу, щоб вийти...");
            Console.ReadKey();
        }
    }
}
