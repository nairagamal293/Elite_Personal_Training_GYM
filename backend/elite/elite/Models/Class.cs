namespace elite.Models
{
    public class Class
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Duration { get; set; }
        public int TrainerId { get; set; }
        public int MaxCapacity { get; set; }
        public decimal Price { get; set; }

        public virtual Trainer Trainer { get; set; }
        public virtual ICollection<ClassSchedule> Schedules { get; set; }
    }

}
