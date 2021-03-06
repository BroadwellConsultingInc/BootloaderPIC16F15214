/*!
\file main.c
\mainpage PIC16F15214Bootloader
Latest version can be found at:
https://github.com/BroadwellConsultingInc/BootloaderPIC16F15214
\copyright
MIT License

Copyright (c) 2020 Broadwell Consulting Inc.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

Video Tutorial
--------------
\htmlonly
<iframe width="560" height="315" src="https://www.youtube.com/embed/OfW4hHFVy3U" frameborder="0" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture" allowfullscreen></iframe>
\endhtmlonly
Note that the Rx pin has moved from pin 3 (RA4) to pin 4 (RA3) since this video was made.  The user may select their own pins by changing the TRISA and PPS configuration.

Overview
---------
The main program implements a bootloader that runs without 
interrupts.  It was developed on MPLAB X v5.40 and XC8
v2.31 running in PRO mode, optimization level s.
Compiling the bootloader under -O2 (free) optimization
levels will result in larger sizes, and addresses
that are hard coded, such as the reset and 
interrupt jump vectors at 0x140 and 0x144 may need to be adjusted.

 The bootloader consists of a number of modules:

1-  Initialization

2-  Stay in Boot Check

If stay in boot (else jump to App)...

3-  Wait for boot handshake word

4-  Erase entire application area

5-  Send a 'W' command and write 64 received bytes (32 words) to flash

6-  Repeat (5.) until all words have been received 

7-  Read entire application flash area out through serial port

8-  While(1) loop waiting for power cycle reset.


 Logic for downloading application image:
------------------------------------
(A C# .Net Core app which performs these steps can be downloaded from
https://github.com/BroadwellConsultingInc/BootloaderPIC16F15214/tree/main/PIC16F15214BootloaderApp )

1- Load the hex file.  Crop to the Application range 0x140-0xFFF (16 bit word addresses) or 0x280-0x1FFE (8 bit addresses), inclusive Fill any empty locations in the hex file inside the application space with 0x3FFF .

2- Connect at 115,200 / 8-N-1

3- Optionally wait for a EBOOTx>> string to indicate bootloader entry.  x indicates reason for bootload mode.

4- This is sent once after reset if the bootloader stays in boot mode

5- Send the sequence 0x52, 0xA3, 0x4D, 0xF6 to start the download sequence.  If the bootloader does not respond with 'e' then send again until an 'e' follows.

The bootloader will Erase the entire Application Area.  In testing this took about 300mS.  

6- Wait for a 'W' to be sent indicating the bootloader is ready to begin writing.

7- Send 64 bytes of data to be programmed, starting at address 0x140.

8- Wait for another 'W' response, and send the next 64 bytes.  Continue this way until the entire application area is sent.

The bootloader will respond with a final 'W', then an 'R' indicating that the bootloader is now reading out flash.

The bootloader will send the entire contents of the application flash area.  

9- Verify this against the hex file to determine if programming was correct.

10- Power cycle the micro to exit boot mode.



Building a downloadable application:
------------------------------------
(A sample application can be found at 
https://github.com/BroadwellConsultingInc/BootloaderPIC16F15214/tree/main/ApplicationPIC16F15214.X )

In order to build an application that can be downloaded with this bootloader, the application must be shifted up 0x140 words.  
This is done in MPLAB X v5.40 by choosing project properties/XC8 global options / XC8 Global Options/ XC8 Linker / Additional Options and putting 0x140 in the Codeoffset window.

The final word of the application's flash must be 0x14B7 This can be achieved by adding an assembly .s file that contains the following (code also sets a stub reset vector for IDE debugging):
\code{.unparsed}
psect   loadCompleteMarker,local,class=CODE,abs ; PIC10/12/16

 ORG 1FFEh   
   DW 14B7h    ; here we use a symbol defined via xc.inc
psect  resetstub,global,class=CODE,delta=2,abs
 ORG 0
   pagesel 140
   GOTO 140
 
 ORG 4
   pagesel 144h
   GOTO 144h
\endcode

This area must be reserved by the linker, or it will override any code in this area.  This is done in MPLAB X v5.40 by choosing project properties/XC8 global options / XC8 Global Options/ XC8 Linker / Memory Model and putting 
-FFF-FFF
in the "ROM ranges" box to remove that space from available area for allocation.

Before using this bootloader you should consider the device configuration settings in device_config.c  .  These are the settings that will be used for both the bootloader and application.  The application will NOT download new configuration byte settings.  For instance, if your application requires a permanently turned on watchdog, then the config bits (and this bootloader) will need to be modified.  

The built application now has no reset vector or interrupt vector at the address 0x0000 / 0x0004 in the micro.  This will make the code unusable if it is downloaded via a programmer/debugger rather than the bootloader, making debugging with an ICD or PicKit difficult.  To solve this problem stub vectors are added in the assembly snippet above which jump from the natural vector locations to the offset locations in the application.
*/


#include <xc.h>
#include <stdint.h>
#include <stdbool.h>

/// Write Size of a line of flash in words
#define WRITE_FLASH_BLOCKSIZE    32
/// ERASE Size of a line of flash in words
#define ERASE_FLASH_BLOCKSIZE    32

/// End address of flash programming area (exclusive)
#define END_FLASH                0x1000

/// The address (in words) in flash where the Application's reset vector will be placed.
#define  NEW_RESET_VECTOR        0x140 

/// The address (in words) in flash where the Application's interrupt handler or interrupt handler goto statement will be placed.
#define  NEW_INTERRUPT_VECTOR    (NEW_RESET_VECTOR + 4)
#define _str(x)  #x
#define str(x)  _str(x)




// The interrupt vector on a PIC16F is located at
// address 0x0004. 
// The following function will be located
// at the interrupt vector and will contain a jump to
// the new application interrupt vector

    asm ("psect  intentry,global,class=CODE,delta=2");
    asm ("pagesel " str(NEW_INTERRUPT_VECTOR));
    asm ("GOTO " str(NEW_INTERRUPT_VECTOR));


//Function prototypes:
void StartWrite(void);
uint8_t EUSART1_Read(void);
void EUSART1_Write(uint8_t txData);
uint8_t Bootload_Required(void);
void Run_Bootloader(void);

/// \brief Global variable storing reason that the bootloader stayed in boot rather than jumping to the application
/// 
/// Valid values are:
/// * 'L' -  The low byte of the flash end programming magic value was not correct 
/// * 'H' -  The high byte of the flash end programming magic value was not correct 
/// * 'F' -  The first byte of flash in application was not programmed to non-erased value
/// * 'S' -  The Stack Overflow indicator is set (application request to stay in boot)
/// * 'V' -  The system voltage was less than 2.2 volts, indicating an external request to stay in boot.
uint8_t bootloadReason = 0;


/// \brief Main Program
/// The main program contains a flattening of a number of functions
/// created for initialization inside SYSTEM_Initialize by the
/// Microchip code configurator (MCC) tool.  The function calls were removed
/// to save flash space since most functions simply assign values to 
/// registers.  The original function name is commented out but left in to help
/// line comparison software in the case that a comparison is done between this main
/// and a new MCC generation.
/// The Bootloader runs at 16MHz so that it can run down to 1.8V
void main(void)
{
	CPCON = 0xC0;  // Turn on charge pump for low voltage analog operation.
	// initialize the device


	
	// void SYSTEM_Initialize(void)
	{

		//void PIN_MANAGER_Initialize(void)
		{
			/**
			  LATx registers
			  */
			LATA = 0x00;

			/**
			  TRISx registers
			  */
			TRISA = 0x3F;

			/**
			  ANSELx registers
			  */
			ANSELA = 0x07;

			/**
			  WPUx registers
			  */
			//WPUA = 0x00;//Commented out to save flash due to match reset value

			/**
			  ODx registers
			  */
			//ODCONA = 0x00;//Commented out to save flash due to match reset value

			/**
			  SLRCONx registers
			  */
			SLRCONA = 0x37;

			/**
			  INLVLx registers
			  */
			INLVLA = 0x3F;

			RX1PPS = 0x03;   //RA4->EUSART1:RX1;    
		}
		//void OSCILLATOR_Initialize(void)
		{
			// MFOEN disabled; LFOEN disabled; ADOEN disabled; HFOEN disabled; 
			//OSCEN = 0x00;//Commented out to save flash due to match reset value
			// FRQ 16_MHz; 
			OSCFRQ = 0x04;
			// TUN 0; 
			//OSCTUNE = 0x00;//Commented out to save flash due to match reset value
		}

		//   void FVR_Initialize(void)
		{
			// FVREN enabled; ADFVR off; 
			FVRCON = 0x81;
		}
		// ADC_Initialize();
		{
			// ADFM left; ADPREF VDD; ADCS Frc; 
			ADCON1 = 0x70;
			ADCON0 = (0x1E << 2) | 0x01;  // Turn on, select FVR channel.
		}
		//void EUSART1_Initialize(void)
		{
			// Set the EUSART1 module to the options selected in the user interface.

			// ABDOVF no_overflow; SCKP Non-Inverted; BRG16 16bit_generator; WUE disabled; ABDEN disabled; 
			BAUD1CON = 0x08;

			// SPEN enabled; RX9 8-bit; CREN enabled; ADDEN disabled; SREN disabled; 
			RC1STA = 0x90;

			// TX9 8-bit; TX9D 0; SENDB sync_break_complete; TXEN enabled; SYNC asynchronous; BRGH hi_speed; CSRC slave; 
			TX1STA = 0x24;

			// SPBRGL 34; 
			SP1BRGL = 34;

			// SPBRGH 0; 
			// SP1BRGH = 0x00; //Commented out to save flash due to match reset value
		}

        
        bootloadReason = Bootload_Required ();
        
		if (bootloadReason != 0)
		{
			Run_Bootloader ();      
		}
       
        
		//Not bootloading.  Set up for jump to app.
		STKPTR = 0x1F;
		BSR = 0;
		asm ("pagesel " str(NEW_RESET_VECTOR));
		asm ("goto  "  str(NEW_RESET_VECTOR));
	}


}

/// An array of bytes that is treated like a FIFO by the code.  This array stores the last 4 bytes received and compares them to a magic value to enter the bootloader.
uint8_t startBytes[4];

/// The bootloader programming executable.  This function is called if the Bootload_Required function indicates a bootload is required.
void Run_Bootloader()
{ 
	TRISA = 0x3B;   // Enable TX output
	RA2PPS = 0x05;   //RA2->EUSART1:TX1;    
	TX1REG = 'E'; // Indicate Bootloader Entry.  First transmit, so no need to delay.
    EUSART1_Write('B');
    EUSART1_Write('O');
    EUSART1_Write('O');
    EUSART1_Write('T');
    EUSART1_Write(bootloadReason);
    EUSART1_Write('>');
    EUSART1_Write('>');
    startBytes[3] = 0;
    
	while (startBytes[0] != 0x52 ||
			startBytes[1] != 0xA3 ||
			startBytes [2] != 0x4D ||
			startBytes[3] != 0xF6)
	{
		startBytes[0] = startBytes[1];
		startBytes[1] = startBytes[2];
		startBytes[2] = startBytes[3];
		startBytes[3] = EUSART1_Read();
	}
	TX1REG = 'e';   // Waited for receipt of at least 4 characters, so no need to delay. 

	//void Erase_Flash ()
	{
		NVMADRL = (uint8_t)(NEW_RESET_VECTOR);
		NVMADRH = (uint8_t)(NEW_RESET_VECTOR >>8);
		for (uint16_t i=0; i < 0x1000; i+=32)
		{

			NVMCON1 = 0x94;       // Setup writes
			StartWrite();

			NVMADRL += ERASE_FLASH_BLOCKSIZE;
			if (NVMADRL == 0x00)
			{
				++ NVMADRH;
			}
		}

	}


	TX1REG = 'W';  // Erase takes a long time, so no need for delay.



	// void Write_Flash()
	{

		NVMCON1 = 0xA4;       // Setup writes
		NVMADR = NEW_RESET_VECTOR;


		while ((NVMADRH & 0x10) == 0) // Hard coded to value 0x1000
		{	
			NVMDATL = EUSART1_Read();
			NVMDATH = EUSART1_Read();

			if ((NVMADRL & 0x1F) == 0x1F)  // 32 word boundary
			{
				NVMCON1bits.LWLO = 0;
				StartWrite();
				NVMCON1 = 0xA4;       // Setup writes
				EUSART1_Write('W');
			}
			else
			{
				StartWrite();
			}
			++NVMADR;
		}

		EUSART1_Write('R'); // Use EUSART1_Write because we need delay here.

		//    void Read_Flash()
		{

			NVMCON1 = 0;
			NVMADR = NEW_RESET_VECTOR;  
			while ((NVMADRH & 0x10) == 0) // Hard coded to value 0x1000
			{	      
				NVMCON1bits.RD = 1;
				EUSART1_Write(NVMDATL);
				EUSART1_Write(NVMDATH);
				++NVMADR;
			}            
		}
	}
	while(1);

}

/// \brief  Determine whether we should stay in the bootloader or jump to app
/// Jump to app will happen unless one of the following criteria is present:
/// *  The first location in App space is unprogrammed (0x3FFF)
/// *  The last location is App space is not the magic number 0x14B7
/// *  The last reset was caused by a hardware stack overflow (indication from App that we should stay in boot)
/// *  The voltage on Vdd is less than 2.2V across 2 samples 100ms Apart
uint8_t Bootload_Required ()
{

	// Check to see if the last address is 0x14B7
	{
		NVMADRL = 0xFF;
		NVMADRH = 0xF;
		NVMCON1 = 0;
		NVMCON1bits.RD = 1;
		NVMCON1 = 0;
		if (NVMDATL != 0xB7)
		{
			return('L');
		}

		if (NVMDATH != 0x14)
		{
			return('H');
		}
	}

	// Check to see if the first address is programmed
	{
		NVMADRL = (uint8_t)(NEW_RESET_VECTOR);
		NVMADRH = (uint8_t)(NEW_RESET_VECTOR >>8);
		NVMCON1 = 0;
		NVMCON1bits.RD = 1;
		NVMCON1 = 0;
		if (NVMDATL == 0xFF && NVMDATH == 0x3F)
		{
			return('F');
		}
	}

	//Check to see if last reset was from stack overflow
	{ 
		if (PCON0bits.STKOVF) //Stack overflow - indicator from application to stay in boot mode.
		{
			return ('S');
		}
	}

	// Check to see if Vdd is less than 2.2V
	{ 

		// Start the conversion  A/D was set up in initialization.
		ADCON0bits.GO_nDONE = 1;

		// Wait for the conversion to finish
		while (ADCON0bits.GO_nDONE)
		{
		}
		if (ADRESH > 0x77)  // Upper byte of 1.024/2.2 * 65535
		{
			// First sample was less than 2.2V 
			// Wait 100ms and check again in case the low voltage is
			// due to a slow-rising power supply

			// Use Timer 1 to time delay
			{ 
				TMR1H = 0xF3;  // About 100ms until overflow
				T1CLK = 4; // LFINTOSC
				T1CON = 0x01; // Start timer
				while(!PIR1bits.TMR1IF); // wait for overflow
				T1CON =0;  // Shut off timer.
			}

			ADCON0bits.GO_nDONE = 1; // Convert again.

			// Wait for the conversion to finish
			while (ADCON0bits.GO_nDONE)
			{
			}
			if (ADRESH > 0x77) {return ('V');};
		}
		return (0);
	}
}


/// Unlock and start the write or erase sequence.  See the PIC16F15214 datasheet.
void StartWrite()
{   
    asm ("movlw 0x55 " );
    asm ("movwf " str(BANKMASK(NVMCON2)));
    asm ("movlw 0xAA  " );
    asm ("movwf " str(BANKMASK(NVMCON2)));
    asm ("bsf  "  str(BANKMASK(NVMCON1)) ",1");       // Start the write
    NOP();
    NOP();
}


/// Wait until a byte is avaialble from the UART and return it.
uint8_t EUSART1_Read(void)
{
    while(!PIR1bits.RC1IF); // Delay until there's a byte available
    return RC1REG;
}

/// Wait until buffer space is avaialble in the UART and transmit a byte
void EUSART1_Write(uint8_t txData)
{
    while(0 == PIR1bits.TX1IF);  // Wait until the TXbuffer is empty.
    TX1REG = txData;    // Write the data byte to the USART.
}
/**
 End of File
*/
