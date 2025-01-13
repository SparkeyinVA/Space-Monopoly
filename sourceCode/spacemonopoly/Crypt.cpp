//---------------------------------------------------------------------------
#include <vcl\vcl.h>
#pragma hdrstop

#include "crypt.h"
//---------------------------------------------------------------------------
/*****************************************************************************
 * crypt -- encrypt and decrypt functions used to encrypt and decrypt arrays *
 *          of characters using arrays of characters!                        *
 *                                                                           *
 * Author: David Vivash                                                      *
 * Created: 6-Jul-1998                                                       *
 *****************************************************************************/

//-----------------------------------------------

void encrypt(unsigned char *buffer, unsigned char *passbuf,int size1, int size2)
{
	int j=0; /* Counter for passbuf */
	for (int i=0; i<(size1); ++i)
	{
		//if((buffer[i] += passbuf[j])>255) buffer[i] -= 256;
		if ((256-passbuf[j])<=buffer[i])
		buffer[i]-=(256-passbuf[j]);
		else buffer[i]+=passbuf[j];
		//buffer[i] += passbuf[j];
		++j;
		if (j==size2) j=0;
	}
}

//------------------------------------------
void decrypt(unsigned char *buffer, unsigned char *passbuf,int size1, int size2)
{
	int j=0;
	for (int i=0; i<(size1); ++i)
	{
		//if((buffer[i] -= passbuf[j])<0) buffer[i] += 256;
		if (buffer[i]>passbuf[j])
		buffer[i] -= passbuf[j];
		else buffer[i]+=(256-passbuf[j]);
		//buffer[i] -= passbuf[j];
		++j;
		if (j==size2) j=0;
	}
}
//---------------
