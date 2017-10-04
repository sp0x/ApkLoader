

namespace SharpAdbClient.Proto{
    using System.Text;
    using System;

    public class Touch{
        public const int EVENT_TYPE_TOUCH = 3;
        public const int CODE_X = 0;
        public const int CODE_Y = 1;

        public static string CreateTouch(int x, int y, string inputDev="/dev/input/event1"){
            var sb = new StringBuilder();
            // print_event(event.type, event.code, event.value, print_flags);
            sb.Append($"sendevent {inputDev} 1 330 1;");
            sb.Append($"sendevent {inputDev} {EVENT_TYPE_TOUCH} {CODE_X} {x};");
            sb.Append($"sendevent {inputDev} {EVENT_TYPE_TOUCH} {CODE_Y} {y};");
            sb.Append($"sendevent {inputDev} {EVENT_TYPE_TOUCH} 24 255;");
            sb.Append($"sendevent {inputDev} 0000 0000 00000000;");
            return sb.ToString();
        }
    }
}