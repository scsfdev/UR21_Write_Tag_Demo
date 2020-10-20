namespace UR21_Write_Tag_Demo.Model
{
    public class DataItem
    {
        public string Title
        {
            get;
            private set;
        }

        public DataItem(string title)
        {
            Title = title;
        }
    }
}