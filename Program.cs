using System;
using System.Windows;

namespace BO2InjectorGUI;

public static class Program
{
	[STAThread]
	public static void Main()
	{
		new Application().Run(new MainWindow());
	}
}
